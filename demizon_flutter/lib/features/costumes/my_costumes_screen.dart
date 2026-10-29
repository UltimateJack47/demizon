import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/routes.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import 'costume_logic.dart';
import 'costume_providers.dart';

/// Moje kroje: co čeká na potvrzení, co mám u sebe, můj QR a historie.
/// Protějšek webové stránky `/Admin/Costumes`.
class MyCostumesScreen extends ConsumerWidget {
  const MyCostumesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final mine = ref.watch(myCostumesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Moje kroje')),
      body: RefreshIndicator(
        onRefresh: () async => invalidateCostumeViews(ref),
        child: mine.when(
          data: (data) => _Body(data: data),
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => ListView(
            children: [
              Padding(
                padding: const EdgeInsets.all(20),
                child: Text(apiErrorText(e, 'Krojárku se nepodařilo načíst.'),
                    style: const TextStyle(color: DemizonColors.error)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Body extends ConsumerWidget {
  const _Body({required this.data});

  final MyCostumes data;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final history = ref.watch(myCostumeHistoryProvider);
    final format = DateFormat('d. M. yyyy');

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        for (final transfer in data.incoming)
          Card(
            color: DemizonColors.warning.withValues(alpha: 0.15),
            child: ListTile(
              leading: const Icon(Icons.inventory_2, color: DemizonColors.warning),
              title: Text('Od ${transfer.fromName}: ${transfer.lines.length} ks'),
              subtitle: const Text('Zkontroluj fotky a potvrď převzetí.'),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push(AppRoutes.costumeTransferFor(transfer.id)),
            ),
          ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Mám u sebe', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                if (data.holdings.isEmpty)
                  Text('Nemáš u sebe nic z krojárky.', style: TextStyle(color: context.palette.textSecondary)),
                for (final holding in data.holdings)
                  ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    title: Text('${holding.item.code}  ${holding.item.description}'),
                    trailing: holding.quantity > 1 ? Text('${holding.quantity} ks') : null,
                  ),
                if (data.holdings.isNotEmpty)
                  Align(
                    alignment: Alignment.centerRight,
                    child: OutlinedButton.icon(
                      onPressed: () => context.push(AppRoutes.costumeHandoverFor(HandoverMode.peer.query)),
                      icon: const Icon(Icons.swap_horiz),
                      label: const Text('Předat kolegovi'),
                    ),
                  ),
              ],
            ),
          ),
        ),
        if (data.outgoing.isNotEmpty)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Moje rozpracované předávky', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  for (final transfer in data.outgoing)
                    ListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      title: Text('→ ${transfer.toName} · ${transfer.lines.length} ks'),
                      subtitle: Text(transferStatusLabel(transfer.status)),
                      onTap: () => context.push(transfer.isDraft
                          ? AppRoutes.costumeDraftFor(transfer.id)
                          : AppRoutes.costumeTransferFor(transfer.id)),
                    ),
                ],
              ),
            ),
          ),
        const _QrCard(),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Historie', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                ...history.when(
                  data: (list) => list.where((t) => !t.isDraft).isEmpty
                      ? [Text('Zatím žádné předávky.', style: TextStyle(color: context.palette.textSecondary))]
                      : [
                          for (final t in list.where((t) => !t.isDraft).take(20))
                            ListTile(
                              dense: true,
                              contentPadding: EdgeInsets.zero,
                              title: Text('${t.fromName} → ${t.toName}'),
                              subtitle: Text(
                                  '${format.format(t.createdAt.toLocal())} · ${transferStatusLabel(t.status)} · ${t.lines.map((l) => l.item.code).join(', ')}'),
                              onTap: () => context.push(AppRoutes.costumeTransferFor(t.id)),
                            ),
                        ],
                  loading: () => [const LinearProgressIndicator()],
                  error: (_, __) => [const Text('Historii se nepodařilo načíst.')],
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _QrCard extends ConsumerWidget {
  const _QrCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final qr = ref.watch(myCostumeQrProvider);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Můj QR pro krojárku', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
            const SizedBox(height: 4),
            Text(
              'Ukaž ho při předávce — správce nebo kolega podle něj pozná, komu kusy předává. '
              'Převzetí pak potvrzuješ ty sám(a) tady v aplikaci.',
              style: TextStyle(fontSize: 12, color: context.palette.textSecondary),
            ),
            const SizedBox(height: 12),
            Center(
              child: qr.when(
                data: (bytes) => Image.memory(bytes, width: 220, height: 220),
                loading: () => const SizedBox(width: 220, height: 220, child: Center(child: CircularProgressIndicator())),
                error: (e, _) => Text(apiErrorText(e, 'QR se nepodařilo načíst.')),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
