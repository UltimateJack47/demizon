import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/routes.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'events_controller.dart';

/// Přepis `Demizon.Maui/Pages/EventsPage.xaml` + `ViewModels/EventsViewModel.cs`.
class EventsScreen extends ConsumerWidget {
  const EventsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final events = ref.watch(eventsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Akce')),
      floatingActionButton: FloatingActionButton(
        onPressed: () => context.push(AppRoutes.eventCreate),
        backgroundColor: DemizonColors.primary,
        foregroundColor: DemizonColors.textOnPrimary,
        child: const Icon(Icons.add),
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.read(eventsProvider.notifier).refresh(),
        child: events.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _messageState(
            context,
            icon: '⚠️',
            // Text z EventsViewModel.LoadAsync.
            text: 'Nepodařilo se načíst akce.',
          ),
          data: (list) => list.isEmpty
              ? _messageState(
                  context,
                  icon: '📅',
                  text: 'Žádné nadcházející akce',
                )
              : _EventList(events: list),
        ),
      ),
    );
  }

  /// Prázdný / chybový stav. Musí zůstat scrollovatelný, jinak by
  /// `RefreshIndicator` neměl za co tahat.
  Widget _messageState(
    BuildContext context, {
    required String icon,
    required String text,
  }) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(icon, style: const TextStyle(fontSize: 48)),
                const SizedBox(height: 12),
                Text(
                  text,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: DemizonColors.textSecondary,
                      ),
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _EventList extends StatelessWidget {
  const _EventList({required this.events});

  final List<Event> events;

  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      physics: const AlwaysScrollableScrollPhysics(),
      // Hlavička + patka (v MAUI CollectionView.Header / .Footer).
      itemCount: events.length + 2,
      itemBuilder: (context, index) {
        if (index == 0) {
          return Padding(
            padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
            child: Text(
              'Nadcházející akce',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: DemizonColors.textPrimary,
                  ),
            ),
          );
        }
        if (index == events.length + 1) {
          return const SizedBox(height: 80);
        }
        return _EventCard(event: events[index - 1]);
      },
    );
  }
}

class _EventCard extends StatelessWidget {
  const _EventCard({required this.event});

  final Event event;

  @override
  Widget build(BuildContext context) {
    final card = Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.push(AppRoutes.eventDetailFor(event.id)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      event.name,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                        color: DemizonColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    _IconLine(icon: '📅', text: formatDate(event.dateFrom)),
                    if (event.place != null && event.place!.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      _IconLine(icon: '📍', text: event.place!),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Container(
                width: 14,
                height: 14,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: DemizonTheme.attendanceColor(
                    event.myAttendance?.status,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );

    // MAUI: DataTrigger na IsCancelled → Opacity 0.45.
    return event.isCancelled ? Opacity(opacity: 0.45, child: card) : card;
  }
}

class _IconLine extends StatelessWidget {
  const _IconLine({required this.icon, required this.text});

  final String icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Text(icon, style: const TextStyle(fontSize: 14)),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            text,
            style: const TextStyle(
              fontSize: 13,
              color: DemizonColors.textSecondary,
            ),
          ),
        ),
      ],
    );
  }
}
