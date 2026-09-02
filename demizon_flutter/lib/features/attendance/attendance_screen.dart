import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'attendance_controller.dart';

/// Přepis `Pages/Attendance/AttendancePage.xaml` + `AttendanceViewModel.cs`.
///
/// Hlavní obrazovka docházky: měsíční seznam akcí a zkoušek se stavem
/// docházky přihlášeného uživatele.
///
/// **Neportováno z MAUI:** `SwipeGestureInterceptor` (104 ř. + hook do
/// `MainActivity`) — `RefreshView` s `CollectionView` uvnitř tam požíraly
/// gesta, takže se swipe musel odchytávat na úrovni Activity.
/// Ve Flutteru stačí `GestureDetector(onHorizontalDragEnd:)` nad obsahem:
/// `ListView` scrolluje svisle, vodorovný drag mu gesture aréna nechá.
/// Neportuje se ani `LongPressBehavior` ani pětinásobné skrývání toolbaru.
class AttendanceScreen extends ConsumerStatefulWidget {
  const AttendanceScreen({super.key, this.initialMonth});

  /// Volitelný výchozí měsíc (MAUI: query parametry `year` / `month`).
  final YearMonth? initialMonth;

  @override
  ConsumerState<AttendanceScreen> createState() => _AttendanceScreenState();
}

class _AttendanceScreenState extends ConsumerState<AttendanceScreen> {
  late YearMonth _month = widget.initialMonth ?? YearMonth.now();

  void _goToPreviousMonth() => setState(() => _month = _month.previous);

  void _goToNextMonth() => setState(() => _month = _month.next);

  /// Swipe doleva = další měsíc, doprava = předchozí.
  void _onHorizontalDragEnd(DragEndDetails details) {
    final velocity = details.primaryVelocity ?? 0;
    if (velocity == 0) return;
    if (velocity < 0) {
      _goToNextMonth();
    } else {
      _goToPreviousMonth();
    }
  }

  @override
  Widget build(BuildContext context) {
    final events = ref.watch(attendanceProvider(_month));

    return Scaffold(
      appBar: AppBar(title: const Text('Docházka')),
      body: Column(
        children: [
          _Header(
            month: _month,
            events: events.valueOrNull ?? const [],
            onPrevious: _goToPreviousMonth,
            onNext: _goToNextMonth,
          ),
          Expanded(
            child: GestureDetector(
              onHorizontalDragEnd: _onHorizontalDragEnd,
              child: RefreshIndicator(
                onRefresh: () =>
                    ref.read(attendanceProvider(_month).notifier).refresh(),
                child: events.when(
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (_, __) => const _ScrollableMessage(
                    // MAUI: AttendanceViewModel.LoadAsync catch → ErrorMessage
                    child: Text(
                      'Nepodařilo se načíst docházku.',
                      style: TextStyle(
                        color: DemizonColors.error,
                        fontSize: 14,
                      ),
                    ),
                  ),
                  data: (items) => items.isEmpty
                      ? const _ScrollableMessage(child: _EmptyState())
                      : _EventList(items: items),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Hlavička: navigace mezi měsíci + souhrn a tlačítka Přehled / Statistiky.
class _Header extends StatelessWidget {
  const _Header({
    required this.month,
    required this.events,
    required this.onPrevious,
    required this.onNext,
  });

  final YearMonth month;
  final List<Event> events;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Material(
      color: isDark
          ? DemizonColors.cardBackgroundDark
          : DemizonColors.cardBackground,
      elevation: 2,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Column(
          children: [
            Row(
              children: [
                IconButton(
                  onPressed: onPrevious,
                  icon: const Icon(Icons.chevron_left),
                  tooltip: 'Předchozí měsíc',
                ),
                Expanded(
                  child: Text(
                    month.label,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                IconButton(
                  onPressed: onNext,
                  icon: const Icon(Icons.chevron_right),
                  tooltip: 'Další měsíc',
                ),
              ],
            ),
            // MAUI: IsVisible="{Binding HasEvents}"
            if (events.isNotEmpty) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  const Text(
                    '✓',
                    style: TextStyle(
                      color: DemizonColors.attendanceYes,
                      fontWeight: FontWeight.bold,
                      fontSize: 14,
                    ),
                  ),
                  const SizedBox(width: 4),
                  Text.rich(
                    TextSpan(
                      children: [
                        TextSpan(
                          text: '${events.attendedCount}',
                          style:
                              const TextStyle(fontWeight: FontWeight.bold),
                        ),
                        const TextSpan(text: ' / '),
                        TextSpan(text: '${events.totalCount}'),
                      ],
                    ),
                    style: const TextStyle(fontSize: 14),
                  ),
                  const Spacer(),
                  TextButton(
                    onPressed: () =>
                        context.push(attendanceOverviewPath(month)),
                    child: const Text(
                      '👥 Přehled',
                      style: TextStyle(fontSize: 13),
                    ),
                  ),
                  TextButton(
                    onPressed: () => context.push(attendanceStatsPath(month)),
                    child: const Text(
                      '📊 Statistiky',
                      style: TextStyle(fontSize: 13),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _EventList extends StatelessWidget {
  const _EventList({required this.items});

  final List<Event> items;

  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      // MAUI mělo Header/Footer BoxView 8 / 20.
      padding: const EdgeInsets.only(top: 8, bottom: 20),
      physics: const AlwaysScrollableScrollPhysics(),
      itemCount: items.length,
      itemBuilder: (context, index) => _EventCard(event: items[index]),
    );
  }
}

class _EventCard extends StatelessWidget {
  const _EventCard({required this.event});

  final Event event;

  void _open(BuildContext context) {
    // MAUI: Id == 0 → zkouška (nese ji datum), jinak akce.
    context.push(
      event.id == 0
          ? rehearsalDetailPath(event.dateFrom)
          : eventDetailPath(event.id),
    );
  }

  /// Dlouhý stisk zobrazí poznámku k mojí docházce.
  /// MAUI to řešilo `LongPressBehavior` + `LongPressTracker`, aby se tap
  /// a long-press nepraly. `GestureDetector` je rozlišuje sám.
  void _showNote(BuildContext context) {
    final comment = event.myAttendance?.comment;
    if (comment == null || comment.trim().isEmpty) return;
    showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Poznámka'),
        content: Text(comment),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final comment = event.myAttendance?.comment;
    final hasComment = comment != null && comment.trim().isNotEmpty;

    return Opacity(
      // DataTrigger IsCancelled → Opacity 0.45
      opacity: event.isCancelled ? 0.45 : 1,
      child: Card(
        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
          // DataTrigger IsRehearsal=False → zlatý rámeček.
          side: event.isRehearsal
              ? BorderSide.none
              : const BorderSide(color: DemizonColors.eventGold, width: 2),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: () => _open(context),
          onLongPress: () => _showNote(context),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                _DateBox(date: event.dateFrom),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        event.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                      if (!event.isRehearsal) ...[
                        const SizedBox(height: 2),
                        const _Badge(
                          text: '⭐ Akce',
                          color: DemizonColors.eventGold,
                        ),
                      ],
                      if (event.place != null &&
                          event.place!.trim().isNotEmpty) ...[
                        const SizedBox(height: 2),
                        Row(
                          children: [
                            const Text('📍', style: TextStyle(fontSize: 11)),
                            const SizedBox(width: 4),
                            Expanded(
                              child: Text(
                                event.place!,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  fontSize: 12,
                                  color: isDark
                                      ? DemizonColors.primaryLight
                                      : DemizonColors.textSecondary,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ],
                      if (event.isCancelled) ...[
                        const SizedBox(height: 2),
                        const _Badge(
                          text: 'Zrušeno',
                          color: DemizonColors.attendanceNo,
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
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
                    // Jen indikátor, že poznámka existuje — text se čte
                    // dlouhým stiskem karty (stejně jako v MAUI).
                    if (hasComment) ...[
                      const SizedBox(height: 2),
                      const Text('📝', style: TextStyle(fontSize: 9)),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _DateBox extends StatelessWidget {
  const _DateBox({required this.date});

  final DateTime date;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color:
            isDark ? DemizonColors.warmBeigeDark : DemizonColors.warmBeige,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            DateFormat('dd').format(date),
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              color: DemizonColors.primary,
            ),
          ),
          Text(
            DateFormat('MMM', 'cs').format(date),
            style: const TextStyle(
              fontSize: 11,
              color: DemizonColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.text, required this.color});

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        text,
        style: const TextStyle(
          color: DemizonColors.textOnPrimary,
          fontSize: 10,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return const Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('📋', style: TextStyle(fontSize: 48)),
        SizedBox(height: 12),
        Text(
          'V tomto měsíci nejsou žádné akce',
          style: TextStyle(fontSize: 16, color: DemizonColors.textSecondary),
        ),
      ],
    );
  }
}

/// Obal, který udrží obsah „potažitelný“ i když nic nescrolluje —
/// jinak by `RefreshIndicator` neměl co táhnout.
class _ScrollableMessage extends StatelessWidget {
  const _ScrollableMessage({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: child,
            ),
          ),
        ),
      ),
    );
  }
}
