import 'package:demizon/core/routes.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'dances_controller.dart';

/// Přepis `Pages/DancesPage.xaml`.
///
/// Vyhledávací pole + seznam karet + pull-to-refresh. MAUI `RefreshView`
/// odpovídá `RefreshIndicator`, `CollectionView.EmptyView` prázdnému stavu.
class DancesScreen extends ConsumerStatefulWidget {
  const DancesScreen({super.key});

  @override
  ConsumerState<DancesScreen> createState() => _DancesScreenState();
}

class _DancesScreenState extends ConsumerState<DancesScreen> {
  late final TextEditingController _searchController;

  @override
  void initState() {
    super.initState();
    _searchController = TextEditingController(
      text: ref.read(danceSearchProvider),
    );
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final dances = ref.watch(filteredDancesProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Tance')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Hledat tance...',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _searchController.clear();
                          ref.read(danceSearchProvider.notifier).state = '';
                        },
                      ),
              ),
              onChanged: (value) =>
                  ref.read(danceSearchProvider.notifier).state = value,
            ),
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () => ref.read(dancesProvider.notifier).refresh(),
              child: dances.when(
                data: (items) => _DancesList(dances: items),
                loading: () => const Center(child: CircularProgressIndicator()),
                // MAUI: `ErrorMessage = "Nepodařilo se načíst tance."`
                // (`DancesViewModel.cs:40`).
                error: (_, __) => const _ScrollableMessage(
                  text: 'Nepodařilo se načíst tance.',
                  color: DemizonColors.error,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _DancesList extends StatelessWidget {
  const _DancesList({required this.dances});

  final List<Dance> dances;

  @override
  Widget build(BuildContext context) {
    if (dances.isEmpty) {
      return const _ScrollableMessage(text: 'Žádné tance k zobrazení');
    }

    return ListView.builder(
      padding: const EdgeInsets.symmetric(vertical: 6),
      itemCount: dances.length,
      itemBuilder: (context, index) => _DanceCard(dance: dances[index]),
    );
  }
}

class _DanceCard extends StatelessWidget {
  const _DanceCard({required this.dance});

  final Dance dance;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.push(AppRoutes.danceDetailFor(dance.id)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                dance.name,
                style: const TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.bold,
                  color: DemizonColors.textPrimary,
                ),
              ),
              if (dance.region != null && dance.region!.isNotEmpty) ...[
                const SizedBox(height: 4),
                Row(
                  children: [
                    const Text('📍', style: TextStyle(fontSize: 13)),
                    const SizedBox(width: 4),
                    Expanded(
                      child: Text(
                        dance.region!,
                        style: const TextStyle(
                          fontSize: 13,
                          color: DemizonColors.textSecondary,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
              if (dance.videos.isNotEmpty) ...[
                const SizedBox(height: 4),
                Text(
                  '🎥 ${dance.videos.length} videí',
                  style: const TextStyle(
                    fontSize: 12,
                    color: DemizonColors.textSecondary,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// Zpráva uprostřed, ale ve scrollovatelném rodiči — jinak by pull-to-refresh
/// nefungoval nad prázdným/chybovým stavem.
class _ScrollableMessage extends StatelessWidget {
  const _ScrollableMessage({required this.text, this.color});

  final String text;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Center(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 40),
              child: Text(
                text,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 14,
                  color: color ?? DemizonColors.textSecondary,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
