import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/providers.dart';
import '../../core/routes.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import 'costume_logic.dart';
import 'costume_providers.dart';
import 'widgets/costume_photo_thumb.dart';

/// Detail předávky: kusy s fotkami, potvrzení / odmítnutí příjemcem, zrušení.
/// Sem vede i ťuknutí na notifikaci „Převzetí krojů k potvrzení“.
class CostumeTransferScreen extends ConsumerWidget {
  const CostumeTransferScreen({required this.transferId, super.key});

  final int transferId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final transfer = ref.watch(costumeTransferProvider(transferId));

    return Scaffold(
      appBar: AppBar(title: const Text('Předávka')),
      body: transfer.when(
        data: (t) => _Body(transfer: t),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Text(apiErrorText(e, 'Předávku se nepodařilo načíst.'),
                style: const TextStyle(color: DemizonColors.error)),
          ),
        ),
      ),
    );
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body({required this.transfer});

  final CostumeTransfer transfer;

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  bool _busy = false;

  CostumeTransfer get t => widget.transfer;

  Future<void> _run(Future<void> Function() action, String success, String failure) async {
    setState(() => _busy = true);
    try {
      await action();
      invalidateCostumeViews(ref, transferId: t.id);
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(success)));
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(apiErrorText(e, failure))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _reject() async {
    final reason = await showDialog<String>(context: context, builder: (_) => const _RejectDialog());
    if (reason == null) return;
    await _run(
      () => ref.read(apiClientProvider).rejectCostumeTransfer(t.id, RejectCostumeTransferRequest(reason: reason)),
      'Převzetí odmítnuto.',
      'Odmítnutí se nepodařilo.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(authControllerProvider).session;
    final myId = session?.memberId;
    final isKeeper = session?.roles.contains(kCostumeKeeperRole) ?? false;
    final isRecipient = t.to != null ? t.to!.memberId == myId : isKeeper;
    final canCancel = t.isOpen && (t.createdBy.memberId == myId || isKeeper);
    final format = DateFormat('d. M. yyyy H:mm');
    final hasStale = t.lines.expand((l) => l.photos).any((p) => p.isStale);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('${t.fromName} → ${t.toName}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                const SizedBox(height: 4),
                Chip(label: Text(transferStatusLabel(t.status))),
                Text('Založil(a) ${t.createdBy.fullName}, ${format.format(t.createdAt.toLocal())}',
                    style: TextStyle(fontSize: 12, color: context.palette.textSecondary)),
                if (t.note != null) Padding(padding: const EdgeInsets.only(top: 8), child: Text(t.note!)),
                if (t.rejectionReason != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text('Důvod odmítnutí: ${t.rejectionReason}', style: const TextStyle(color: DemizonColors.error)),
                  ),
                if (isRecipient && t.isPending) ...[
                  if (hasStale)
                    const Padding(
                      padding: EdgeInsets.only(top: 12),
                      child: Text(
                        'Některá fotka je podle fotoaparátu starší, než by u předávky měla být (⚠). '
                        'Než potvrdíš, ověř si stav kusu sám(a).',
                        style: TextStyle(color: DemizonColors.warning),
                      ),
                    ),
                  const Padding(
                    padding: EdgeInsets.only(top: 12),
                    child: Text('Potvrzením převezmeš odpovědnost za kusy ve stavu, jaký je na fotkách.'),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: [
                      FilledButton.icon(
                        onPressed: _busy
                            ? null
                            : () => _run(() => ref.read(apiClientProvider).confirmCostumeTransfer(t.id),
                                'Převzetí potvrzeno.', 'Potvrzení se nepodařilo.'),
                        icon: const Icon(Icons.check),
                        label: const Text('Potvrzuji převzetí'),
                      ),
                      OutlinedButton(onPressed: _busy ? null : _reject, child: const Text('Odmítnout')),
                    ],
                  ),
                ],
                if (t.isDraft && t.createdBy.memberId == myId)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: FilledButton(
                      onPressed: () => context.pushReplacement(AppRoutes.costumeDraftFor(t.id)),
                      child: const Text('Pokračovat v předávce'),
                    ),
                  ),
                if (canCancel)
                  TextButton(
                    onPressed: _busy
                        ? null
                        : () => _run(() => ref.read(apiClientProvider).cancelCostumeTransfer(t.id),
                            'Předávka zrušena.', 'Zrušení se nepodařilo.'),
                    child: const Text('Zrušit předávku', style: TextStyle(color: DemizonColors.error)),
                  ),
              ],
            ),
          ),
        ),
        for (final line in t.lines)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('${line.item.code}  ${line.item.description}${line.quantity > 1 ? ' · ${line.quantity} ks' : ''}',
                      style: const TextStyle(fontWeight: FontWeight.bold)),
                  if (line.conditionNote != null)
                    Text('Stav: ${line.conditionNote}', style: TextStyle(color: context.palette.textSecondary)),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [for (final photo in line.photos) CostumePhotoThumb(transferId: t.id, photo: photo)],
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _RejectDialog extends StatefulWidget {
  const _RejectDialog();

  @override
  State<_RejectDialog> createState() => _RejectDialogState();
}

class _RejectDialogState extends State<_RejectDialog> {
  final _controller = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Odmítnout převzetí'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Kusy zůstanou předávajícímu a správce dostane tvůj důvod.'),
          const SizedBox(height: 8),
          TextField(
            controller: _controller,
            maxLines: 3,
            maxLength: 1000,
            decoration: InputDecoration(labelText: 'Důvod', errorText: _error, border: const OutlineInputBorder()),
          ),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Zpět')),
        FilledButton(
          onPressed: () {
            final reason = _controller.text.trim();
            if (reason.isEmpty) {
              setState(() => _error = 'Bez důvodu správce neví, co řešit.');
              return;
            }
            Navigator.pop(context, reason);
          },
          child: const Text('Odmítnout'),
        ),
      ],
    );
  }
}
