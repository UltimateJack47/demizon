import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/routes.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import 'costume_logic.dart';
import 'costume_providers.dart';

/// Tab správce krojárky: výdej a příjem se skenem, otevřené předávky, kdo co má
/// a připomínky. Katalog, štítky, údržba a inventura jsou na webu.
class CostumeStoreScreen extends ConsumerStatefulWidget {
  const CostumeStoreScreen({super.key});

  @override
  ConsumerState<CostumeStoreScreen> createState() => _CostumeStoreScreenState();
}

class _CostumeStoreScreenState extends ConsumerState<CostumeStoreScreen> {
  final Set<int> _selected = {};

  Future<void> _remind(List<int>? memberIds, int count) async {
    final message = await showDialog<String>(context: context, builder: (_) => _RemindDialog(count: count));
    if (message == null) return;
    try {
      final result = await ref.read(apiClientProvider).remindCostumeHolders(
            CostumeReminderRequest(memberIds: memberIds, message: message.isEmpty ? null : message),
          );
      if (!mounted) return;
      setState(_selected.clear);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(result.reached == result.recipients
            ? 'Připomínka odešla ${result.recipients} členům.'
            : 'Připomínka odešla ${result.recipients} členům, doručit se podařilo ${result.reached}.'),
      ));
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(apiErrorText(e, 'Připomínku se nepodařilo odeslat.'))));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final overview = ref.watch(costumeStoreProvider);

    return Scaffold(
      body: SafeArea(bottom: false, child: RefreshIndicator(
        onRefresh: () async => invalidateCostumeViews(ref),
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: () => context.push(AppRoutes.costumeHandoverFor(HandoverMode.issue.query)),
                  icon: const Icon(Icons.output),
                  label: const Text('Výdej'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: FilledButton.icon(
                  onPressed: () => context.push(AppRoutes.costumeHandoverFor(HandoverMode.returnToStore.query)),
                  icon: const Icon(Icons.input),
                  label: const Text('Příjem'),
                ),
              ),
            ]),
            const SizedBox(height: 16),
            ...overview.when(
              data: _content,
              loading: () => [const Center(child: CircularProgressIndicator())],
              error: (e, _) => [
                Text(apiErrorText(e, 'Přehled krojárky se nepodařilo načíst.'),
                    style: const TextStyle(color: DemizonColors.error)),
              ],
            ),
          ],
        ),
      )),
    );
  }

  List<Widget> _content(CostumeStoreOverview data) => [
        if (data.open.isNotEmpty) ...[
          const Text('Otevřené předávky', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          for (final t in data.open)
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text('${t.fromName} → ${t.toName}'),
              subtitle: Text('${transferStatusLabel(t.status)} · ${t.lines.map((l) => l.item.code).join(', ')}'),
              onTap: () => context.push(AppRoutes.costumeTransferFor(t.id)),
            ),
          const Divider(),
        ],
        Row(children: [
          Expanded(
            child: Text('Kdo co má (${data.holders.length})',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          ),
          if (data.holders.isNotEmpty) ...[
            TextButton(
              onPressed: _selected.isEmpty ? null : () => _remind(_selected.toList(), _selected.length),
              child: Text('Připomenout (${_selected.length})'),
            ),
            TextButton(
              onPressed: () => _remind(null, data.holders.length),
              child: const Text('Všem'),
            ),
          ],
        ]),
        if (data.holders.isEmpty)
          Text('Všechno je v krojárce.', style: TextStyle(color: context.palette.textSecondary)),
        for (final holder in data.holders)
          CheckboxListTile(
            contentPadding: EdgeInsets.zero,
            value: _selected.contains(holder.member.memberId),
            onChanged: (on) => setState(() => on == true
                ? _selected.add(holder.member.memberId)
                : _selected.remove(holder.member.memberId)),
            title: Text(holder.member.isDeleted ? '${holder.member.fullName} (bývalý člen)' : holder.member.fullName),
            subtitle: Text(holder.holdings
                .map((h) => h.quantity > 1 ? '${h.item.code} (${h.quantity} ks)' : h.item.code)
                .join(', ')),
          ),
      ];
}

class _RemindDialog extends StatefulWidget {
  const _RemindDialog({required this.count});

  final int count;

  @override
  State<_RemindDialog> createState() => _RemindDialogState();
}

class _RemindDialogState extends State<_RemindDialog> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text('Připomenout vrácení (${widget.count})'),
      content: TextField(
        controller: _controller,
        maxLines: 3,
        maxLength: 500,
        decoration: const InputDecoration(
          labelText: 'Zpráva (nepovinná)',
          hintText: 'Např. Do neděle prosím vraťte kroje po hodech.',
          border: OutlineInputBorder(),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Zpět')),
        FilledButton(onPressed: () => Navigator.pop(context, _controller.text.trim()), child: const Text('Odeslat')),
      ],
    );
  }
}
