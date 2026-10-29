import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/providers.dart';
import '../../core/routes.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import 'costume_logic.dart';
import 'costume_providers.dart';
import 'widgets/code_scan_field.dart';
import 'widgets/costume_item_picker.dart';
import 'widgets/costume_photo_thumb.dart';

/// Předávka krojů: nejdřív kusy a protistrana (sken), pak fotka u každého kusu
/// a odeslání. Protějšek webové stránky `/Admin/Costumes/Handover`.
class CostumeHandoverScreen extends ConsumerStatefulWidget {
  const CostumeHandoverScreen({required this.mode, this.transferId, super.key});

  final HandoverMode mode;

  /// Rozpracovaná předávka, ve které se pokračuje fotkami.
  final int? transferId;

  @override
  ConsumerState<CostumeHandoverScreen> createState() => _CostumeHandoverScreenState();
}

class _PendingLine {
  _PendingLine(this.item);

  final CostumeItem item;
  int quantity = 1;
}

class _CostumeHandoverScreenState extends ConsumerState<CostumeHandoverScreen> {
  /// Fotku zmenší už telefon: po mobilních datech nemá jet 8 MB. Server ji stejně
  /// zmenší na 1200 px, tohle je jen strop přenosu.
  static const _maxPhotoSide = 1600.0;

  late HandoverMode _mode = widget.mode;
  final List<_PendingLine> _lines = [];
  CostumeParty? _counterpart;
  CostumeTransfer? _draft;
  String? _error;
  bool _busy = false;
  int? _uploadingLine;

  int get _myId => ref.read(authControllerProvider).session?.memberId ?? 0;

  bool get _isKeeper => ref.read(authControllerProvider).session?.roles.contains(kCostumeKeeperRole) ?? false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_mode != HandoverMode.peer && !_isKeeper) {
        setState(() {
          _mode = HandoverMode.peer;
          _error = 'Vydávat a přijímat do krojárky může jen správce krojárky.';
        });
      }
      if (widget.transferId != null) _loadDraft(widget.transferId!);
    });
  }

  Future<T?> _call<T>(Future<T> Function() action, String failure) async {
    setState(() => _busy = true);
    try {
      return await action();
    } catch (e) {
      if (mounted) setState(() => _error = apiErrorText(e, failure));
      return null;
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  // ---------------------------------------------------------- 1. sběr

  Future<void> _onCollectCode(String text) async {
    setState(() => _error = null);
    final scan = CostumeScan.parse(text);
    if (scan == null) return;
    final api = ref.read(apiClientProvider);

    if (scan.kind == CostumeScanKind.member) {
      final member = await _call(() => api.getCostumeMemberByQr(scan.value),
          'QR kód nepatří žádnému aktivnímu členovi (možná si ho mezitím vygeneroval znovu).');
      if (member == null) return;
      if (_mode != HandoverMode.returnToStore && member.id == _myId) {
        setState(() => _error = 'Sám sobě předat nejde.');
        return;
      }
      setState(() => _counterpart = CostumeParty(memberId: member.id, fullName: member.fullName, isDeleted: false));
      return;
    }

    final card = await _call(() => api.getCostumeItemCard(scan.value),
        'Kus ${scan.value.toUpperCase()} v krojárce neexistuje.');
    if (card == null) return;
    if (_lines.any((l) => l.item.id == card.item.id)) {
      setState(() => _error = '${card.item.code} už v seznamu je.');
      return;
    }

    final outcome = checkCollectedItem(
      mode: _mode,
      card: card,
      myMemberId: _myId,
      counterpartId: _counterpart?.memberId,
    );
    setState(() {
      if (!outcome.isAdded) {
        _error = outcome.error;
        return;
      }
      _counterpart ??= outcome.counterpart;
      _lines.add(_PendingLine(card.item));
    });
  }

  /// Kusy, které jde v tomhle režimu přidat: k výdeji volné v krojárce, k příjmu
  /// co kdo drží (jen vracející, když už je daný), kolegovi co mám já.
  Future<List<CostumePickEntry>> _pickEntries(Set<String> taken, int? holderId) async {
    final api = ref.read(apiClientProvider);
    final entries = switch (_mode) {
      HandoverMode.issue => [
          for (final part in (await api.getCostumeCatalog()).parts)
            for (final stock in part.items)
              if (stock.available > 0) CostumePickEntry(item: stock.item, count: stock.available),
        ],
      HandoverMode.returnToStore => [
          for (final holder in await api.getCostumeHolders())
            if (holderId == null || holder.member.memberId == holderId)
              for (final h in holder.holdings) CostumePickEntry(item: h.item, count: h.quantity, holder: holder.member),
        ],
      HandoverMode.peer => [
          for (final h in (await api.getMyCostumes()).holdings) CostumePickEntry(item: h.item, count: h.quantity),
        ],
    };
    return entries.where((e) => !taken.contains(e.item.code)).toList();
  }

  Future<void> _pick({required bool draft}) async {
    setState(() => _error = null);
    final holderId = draft ? _draft!.from?.memberId : _counterpart?.memberId;
    final taken = draft ? {for (final l in _draft!.lines) l.item.code} : {for (final l in _lines) l.item.code};
    final entries = await _call(
        () => _pickEntries(taken, _mode == HandoverMode.returnToStore ? holderId : null), 'Seznam kusů se nepodařilo načíst.');
    if (entries == null || !mounted) return;
    final picked = await showCostumeItemPicker(context, entries);
    if (picked == null) return;
    if (draft) return _onDraftCode(picked.item.code);
    // Known holder: a batch held by several people would otherwise ask for the returner's QR.
    if (picked.holder != null) setState(() => _counterpart ??= picked.holder);
    await _onCollectCode(picked.item.code);
  }

  Future<void> _createDraft() async {
    final counterpart = _counterpart!;
    final (from, to) = switch (_mode) {
      HandoverMode.issue => (null, counterpart.memberId),
      HandoverMode.returnToStore => (counterpart.memberId, null),
      HandoverMode.peer => (_myId, counterpart.memberId),
    };
    final draft = await _call(
      () => ref.read(apiClientProvider).createCostumeTransfer(CreateCostumeTransferRequest(
            fromMemberId: from,
            toMemberId: to,
            lines: [for (final l in _lines) CostumeLineRequest(code: l.item.code, quantity: l.quantity)],
          )),
      'Předávku se nepodařilo založit.',
    );
    if (draft != null) setState(() => _draft = draft);
  }

  // --------------------------------------------------------- 2. fotky

  Future<void> _loadDraft(int id) async {
    final draft = await _call(() => ref.read(apiClientProvider).getCostumeTransfer(id), 'Předávku se nepodařilo načíst.');
    if (draft == null || !mounted) return;
    if (!draft.isDraft || draft.createdBy.memberId != _myId) {
      context.pushReplacement(AppRoutes.costumeTransferFor(id));
      return;
    }
    setState(() => _draft = draft);
  }

  Future<void> _onDraftCode(String text) async {
    setState(() => _error = null);
    final scan = CostumeScan.parse(text);
    if (scan == null || scan.kind != CostumeScanKind.item) {
      setState(() => _error = 'Tady se skenují jen kusy; příjemce už je daný.');
      return;
    }
    await _call(() => ref.read(apiClientProvider).addCostumeLine(_draft!.id, CostumeLineRequest(code: scan.value)),
        'Kus se nepodařilo přidat.');
    await _loadDraft(_draft!.id);
  }

  /// Jen fotoaparát, ne galerie: fotka stavu má vzniknout u předávky.
  Future<void> _photograph(CostumeTransferLine line) async {
    final picked = await ImagePicker()
        .pickImage(source: ImageSource.camera, maxWidth: _maxPhotoSide, maxHeight: _maxPhotoSide, imageQuality: 85);
    if (picked == null) return;
    setState(() => _uploadingLine = line.id);
    await _call(() => ref.read(apiClientProvider).addCostumePhoto(_draft!.id, line.id, File(picked.path)),
        'Fotku se nepodařilo nahrát. Zkus to znovu.');
    if (mounted) setState(() => _uploadingLine = null);
    await _loadDraft(_draft!.id);
  }

  Future<void> _saveNote(CostumeTransferLine line, String note) async {
    await _call(
      () => ref.read(apiClientProvider).setCostumeLineNote(_draft!.id, line.id, CostumeLineNoteRequest(conditionNote: note)),
      'Poznámku se nepodařilo uložit.',
    );
  }

  Future<void> _removeLine(CostumeTransferLine line) async {
    await _call(() => ref.read(apiClientProvider).removeCostumeLine(_draft!.id, line.id), 'Kus se nepodařilo odebrat.');
    await _loadDraft(_draft!.id);
  }

  Future<void> _submit() async {
    final result = await _call(() => ref.read(apiClientProvider).submitCostumeTransfer(_draft!.id),
        'Předávku se nepodařilo odeslat.');
    if (result == null || !mounted) return;
    invalidateCostumeViews(ref, transferId: _draft!.id);
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(result.status == 'Confirmed'
          ? 'Kusy jsou zpátky v krojárce.'
          : 'Odesláno. Příjemce teď předávku potvrdí ve svém telefonu.'),
    ));
    context.pushReplacement(AppRoutes.costumeTransferFor(_draft!.id));
  }

  Future<void> _cancel() async {
    final ok = await _call(() async {
      await ref.read(apiClientProvider).cancelCostumeTransfer(_draft!.id);
      return true;
    }, 'Předávku se nepodařilo zrušit.');
    if (ok != true || !mounted) return;
    invalidateCostumeViews(ref, transferId: _draft!.id);
    context.pop();
  }

  // ------------------------------------------------------------- UI

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_draft == null ? _mode.title : 'Fotky a odeslání')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_error != null)
            Card(
              color: DemizonColors.error.withValues(alpha: 0.1),
              child: ListTile(
                leading: const Icon(Icons.error_outline, color: DemizonColors.error),
                title: Text(_error!),
                trailing: IconButton(icon: const Icon(Icons.close), onPressed: () => setState(() => _error = null)),
              ),
            ),
          if (_busy) const LinearProgressIndicator(),
          ...(_draft == null ? _collectView() : _draftView(_draft!)),
        ],
      ),
    );
  }

  List<Widget> _collectView() => [
        Text(switch (_mode) {
          HandoverMode.issue => 'Naskenuj kusy, které vydáváš, a QR z profilu člena, který si je bere.',
          HandoverMode.returnToStore => 'Naskenuj vracené kusy. Kdo je vrací, se doplní podle kusu — nebo naskenuj jeho QR.',
          HandoverMode.peer => 'Naskenuj kusy, které máš u sebe a předáváš, a QR z profilu kolegy, který je přebírá.',
        }),
        const SizedBox(height: 12),
        CodeScanField(onCode: _onCollectCode),
        _PickButton(onPressed: _busy ? null : () => _pick(draft: false)),
        const SizedBox(height: 8),
        Text('${_mode == HandoverMode.returnToStore ? 'Vrací' : 'Příjemce'}: '
            '${_counterpart?.fullName ?? '— naskenuj QR z jeho profilu —'}',
            style: const TextStyle(fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        if (_lines.isEmpty) Text('Zatím žádné kusy.', style: TextStyle(color: context.palette.textSecondary)),
        for (final line in _lines)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text('${line.item.code}  ${line.item.description}'),
            subtitle: line.item.isBulk
                ? Row(children: [
                    const Text('Počet: '),
                    IconButton(
                      icon: const Icon(Icons.remove),
                      onPressed: line.quantity > 1 ? () => setState(() => line.quantity--) : null,
                    ),
                    Text('${line.quantity}'),
                    IconButton(icon: const Icon(Icons.add), onPressed: () => setState(() => line.quantity++)),
                  ])
                : null,
            trailing: IconButton(icon: const Icon(Icons.close), onPressed: () => setState(() => _lines.remove(line))),
          ),
        const SizedBox(height: 12),
        FilledButton(
          onPressed: _busy || _lines.isEmpty || _counterpart == null ? null : _createDraft,
          child: const Text('Pokračovat k fotkám'),
        ),
      ];

  List<Widget> _draftView(CostumeTransfer draft) {
    final allPhotographed = draft.lines.every((l) => l.photos.isNotEmpty);
    return [
      Text('${draft.fromName} → ${draft.toName}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
      const SizedBox(height: 4),
      Text(
        'Vyfoť každý kus tak, aby byl vidět jeho stav. ${draft.to == null ? 'Odesláním kusy rovnou přijmeš do krojárky.' : 'Po odeslání je příjemce zkontroluje a potvrdí ve svém telefonu.'}',
        style: TextStyle(color: context.palette.textSecondary),
      ),
      const SizedBox(height: 12),
      CodeScanField(onCode: _onDraftCode, label: 'Přidat další kus'),
      _PickButton(onPressed: _busy ? null : () => _pick(draft: true)),
      const SizedBox(height: 4),
      for (final line in draft.lines)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(children: [
                  Expanded(
                    child: Text(
                        '${line.item.code}  ${line.item.description}${line.quantity > 1 ? ' · ${line.quantity} ks' : ''}',
                        style: const TextStyle(fontWeight: FontWeight.bold)),
                  ),
                  if (line.photos.isEmpty)
                    const Chip(label: Text('chybí fotka'), backgroundColor: Color(0x33F39C12)),
                  IconButton(icon: const Icon(Icons.delete_outline), onPressed: _busy ? null : () => _removeLine(line)),
                ]),
                Wrap(spacing: 8, runSpacing: 8, children: [
                  for (final photo in line.photos) CostumePhotoThumb(transferId: draft.id, photo: photo),
                  OutlinedButton.icon(
                    onPressed: _busy ? null : () => _photograph(line),
                    icon: const Icon(Icons.photo_camera),
                    label: Text(_uploadingLine == line.id ? 'Nahrávám…' : 'Vyfotit'),
                  ),
                ]),
                const SizedBox(height: 8),
                _NoteField(
                  key: ValueKey('note-${line.id}'),
                  initial: line.conditionNote,
                  onSave: (note) => _saveNote(line, note),
                ),
              ],
            ),
          ),
        ),
      const SizedBox(height: 12),
      FilledButton(
        onPressed: _busy || !allPhotographed ? null : _submit,
        child: Text(draft.to == null ? 'Přijmout do krojárky' : 'Odeslat k potvrzení'),
      ),
      TextButton(
        onPressed: _busy ? null : _cancel,
        child: const Text('Zrušit předávku', style: TextStyle(color: DemizonColors.error)),
      ),
    ];
  }
}

class _PickButton extends StatelessWidget {
  const _PickButton({required this.onPressed});

  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: TextButton.icon(onPressed: onPressed, icon: const Icon(Icons.list), label: const Text('Vybrat ze seznamu')),
    );
  }
}

/// Poznámka ke stavu kusu; uloží se při odchodu z pole nebo Enteru, jen když se změnila.
class _NoteField extends StatefulWidget {
  const _NoteField({required this.initial, required this.onSave, super.key});

  final String? initial;
  final Future<void> Function(String note) onSave;

  @override
  State<_NoteField> createState() => _NoteFieldState();
}

class _NoteFieldState extends State<_NoteField> {
  late final _controller = TextEditingController(text: widget.initial);
  final _focus = FocusNode();
  late String _saved = widget.initial ?? '';

  @override
  void initState() {
    super.initState();
    _focus.addListener(() {
      if (!_focus.hasFocus) _save();
    });
  }

  void _save() {
    final note = _controller.text.trim();
    if (note == _saved) return;
    _saved = note;
    widget.onSave(note);
  }

  @override
  void dispose() {
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: _controller,
      focusNode: _focus,
      maxLength: 1000,
      textInputAction: TextInputAction.done,
      decoration: const InputDecoration(
          labelText: 'Poznámka ke stavu (nepovinná)', border: OutlineInputBorder(), isDense: true),
      onSubmitted: (_) => _save(),
      onTapOutside: (_) => _focus.unfocus(),
    );
  }
}
