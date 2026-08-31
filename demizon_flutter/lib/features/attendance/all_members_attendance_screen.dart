import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'all_members_attendance_controller.dart';
import 'attendance_controller.dart';

/// Zlatý akcent sloupců, které jsou akce (ne zkoušky). V MAUI hex `#C9A227`
/// zapsaný přímo v `AllMembersAttendancePage.xaml.cs:183`.
// TODO(verify): sjednotit s `attendance_screen.dart` a přesunout do `DemizonColors`.
const _eventGold = Color(0xFFC9A227);

// Rozměry mřížky — 1:1 z `AllMembersAttendancePage.xaml.cs:13-16`.
const _nameColumnWidth = 130.0;
const _cellWidth = 56.0;
const _rowHeight = 40.0;
const _headerHeight = 60.0;

/// Přepis `Pages/Attendance/AllMembersAttendancePage.xaml` +
/// `AllMembersAttendanceViewModel.cs`.
///
/// Křížová tabulka: členové (řádky) × dny měsíce (sloupce).
///
/// **Neportováno z MAUI:**
/// * celé imperativní skládání mřížky v code-behindu (343 ř. `BuildTable`,
///   `AddHeaderCell`, `AddColumnHeader`, `AddAttendanceCell`) — XAML neuměl
///   datovou mřížku, takže se `Border`y a `Label`y vkládaly ručně do `Grid`u.
///   Tady je tabulka deklarativní.
/// * ruční hit-test přes `Handler.PlatformView` a přepočet obdélníků přes
///   `DisplayMetrics.Density` (`AllMembersAttendancePage.xaml.cs:61-87`),
///   který hlídal, aby vnitřní vodorovný scroll nekradl swipe měsíce.
///   Ve Flutteru to řeší gesture aréna: kde je vnitřní `Scrollable`, vyhraje
///   on, jinde vyhraje vnější `GestureDetector`.
/// * `LongPressTracker` — `GestureDetector` rozliší tap a long-press sám.
class AllMembersAttendanceScreen extends ConsumerStatefulWidget {
  const AllMembersAttendanceScreen({super.key, this.initialMonth});

  /// MAUI: query parametry `year` / `month`.
  final YearMonth? initialMonth;

  @override
  ConsumerState<AllMembersAttendanceScreen> createState() =>
      _AllMembersAttendanceScreenState();
}

class _AllMembersAttendanceScreenState
    extends ConsumerState<AllMembersAttendanceScreen> {
  late YearMonth _month = widget.initialMonth ?? YearMonth.now();

  void _previous() => setState(() => _month = _month.previous);

  void _next() => setState(() => _month = _month.next);

  void _onHorizontalDragEnd(DragEndDetails details) {
    final velocity = details.primaryVelocity ?? 0;
    if (velocity == 0) return;
    if (velocity < 0) {
      _next();
    } else {
      _previous();
    }
  }

  @override
  Widget build(BuildContext context) {
    final data = ref.watch(allMembersAttendanceProvider(_month));
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(title: const Text('Přehled docházky')),
      body: Column(
        children: [
          Material(
            color: isDark
                ? DemizonColors.cardBackgroundDark
                : DemizonColors.cardBackground,
            elevation: 2,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              child: Row(
                children: [
                  IconButton(
                    onPressed: _previous,
                    icon: const Icon(Icons.chevron_left),
                    tooltip: 'Předchozí měsíc',
                  ),
                  Expanded(
                    child: Text(
                      _month.label,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  IconButton(
                    onPressed: _next,
                    icon: const Icon(Icons.chevron_right),
                    tooltip: 'Další měsíc',
                  ),
                ],
              ),
            ),
          ),
          Expanded(
            child: GestureDetector(
              onHorizontalDragEnd: _onHorizontalDragEnd,
              child: data.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (_, __) => const Center(
                  child: Padding(
                    padding: EdgeInsets.all(20),
                    child: Text(
                      'Nepodařilo se načíst přehled docházky.',
                      style:
                          TextStyle(color: DemizonColors.error, fontSize: 14),
                    ),
                  ),
                ),
                data: (state) => state.hasData
                    ? _AttendanceTable(state: state)
                    : const _EmptyState(),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────
// Tabulka
// ─────────────────────────────────────────────────────────────────────────

/// Zamrzlý sloupec jmen + vodorovně scrollovatelná datová část.
/// Obojí je uvnitř jednoho svislého scrollu, takže se řádky nikdy nerozejdou;
/// hlavička sloupců je uvnitř téhož vodorovného scrollu jako data, takže
/// není potřeba synchronizovat dva `ScrollController`y.
class _AttendanceTable extends StatelessWidget {
  const _AttendanceTable({required this.state});

  final AllMembersAttendanceState state;

  @override
  Widget build(BuildContext context) {
    final columns = state.table.columns;
    final members = state.table.members;

    return LayoutBuilder(
      builder: (context, constraints) {
        // Když se celá tabulka vejde, vnitřní scroll se vypne, aby vnější
        // `GestureDetector` mohl přepínat měsíce i nad daty
        // (záměr `IsInsideDataArea` z MAUI, ale bez hit-testu).
        final fits = columns.length * _cellWidth <=
            constraints.maxWidth - _nameColumnWidth;

        return SingleChildScrollView(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                children: [
                  const _HeaderCell(width: _nameColumnWidth, child: Text(
                    'Člen',
                    style: TextStyle(
                      color: DemizonColors.textOnPrimary,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  )),
                  for (var r = 0; r < members.length; r++)
                    _NameCell(name: members[r].fullName, row: r),
                ],
              ),
              Expanded(
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  physics: fits ? const NeverScrollableScrollPhysics() : null,
                  child: Column(
                    children: [
                      Row(
                        children: [
                          for (final column in columns)
                            _ColumnHeader(column: column),
                        ],
                      ),
                      for (var r = 0; r < members.length; r++)
                        Row(
                          children: [
                            for (var c = 0; c < columns.length; c++)
                              _AttendanceCell(
                                state: state,
                                member: members[r],
                                column: columns[c],
                                // Server posílá buňky ve stejném pořadí jako
                                // sloupce; kratší řádek znamená chybějící data.
                                cell: c < members[r].cells.length
                                    ? members[r].cells[c]
                                    : null,
                                row: r,
                              ),
                          ],
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _HeaderCell extends StatelessWidget {
  const _HeaderCell({
    required this.width,
    required this.child,
    this.color = DemizonColors.primary,
    this.onTap,
  });

  final double width;
  final Widget child;
  final Color color;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: width,
        height: _headerHeight,
        color: color,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
        child: child,
      ),
    );
  }
}

class _ColumnHeader extends StatelessWidget {
  const _ColumnHeader({required this.column});

  final MonthlyColumn column;

  @override
  Widget build(BuildContext context) {
    return _HeaderCell(
      width: _cellWidth,
      color: column.isEvent ? _eventGold : DemizonColors.primary,
      // Tap na hlavičku otevře detail akce (hlavička není vázaná na člena).
      onTap: column.eventId == null
          ? null
          : () => context.push(eventDetailPath(column.eventId!)),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            column.label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: DemizonColors.textOnPrimary,
              fontSize: 10,
              fontWeight:
                  column.isEvent ? FontWeight.bold : FontWeight.normal,
            ),
          ),
          Opacity(
            opacity: 0.85,
            child: Text(
              DateFormat('d.M.').format(column.date),
              style: const TextStyle(
                color: DemizonColors.textOnPrimary,
                fontSize: 9,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _NameCell extends StatelessWidget {
  const _NameCell({required this.name, required this.row});

  final String name;
  final int row;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: _nameColumnWidth,
      height: _rowHeight,
      color: _rowBackground(context, row),
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      alignment: Alignment.centerLeft,
      child: Text(
        name,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(fontSize: 12),
      ),
    );
  }
}

class _AttendanceCell extends StatelessWidget {
  const _AttendanceCell({
    required this.state,
    required this.member,
    required this.column,
    required this.cell,
    required this.row,
  });

  final AllMembersAttendanceState state;
  final MemberMonthlyRow member;
  final MonthlyColumn column;
  final MemberCell? cell;
  final int row;

  bool get _hasComment =>
      cell?.comment != null && cell!.comment!.trim().isNotEmpty;

  /// Rozlišení „jsem to já / jsem admin / jsem nikdo“
  /// (`AllMembersAttendancePage.xaml.cs:345-424`).
  void _onTap(BuildContext context) {
    final isMine = state.isCurrentUser(member.memberId);

    if (column.eventId != null) {
      if (isMine) {
        context.push(eventDetailPath(column.eventId!));
        return;
      }
      if (state.isAdmin) {
        context.push(memberAttendanceDetailPath(
          eventId: column.eventId!,
          memberId: member.memberId,
          memberName: member.fullName,
        ));
        return;
      }
    } else {
      if (isMine) {
        context.push(rehearsalDetailPath(column.date));
        return;
      }
      if (state.isAdmin) {
        context.push(memberRehearsalDetailPath(
          date: column.date,
          memberId: member.memberId,
          memberName: member.fullName,
        ));
        return;
      }
    }

    // Nikdo: buď poznámka, nebo vysvětlení, proč nejde editovat.
    if (_hasComment) {
      _showNote(context, 'Poznámka – ${member.fullName}', cell!.comment!);
    } else {
      _showNote(
        context,
        'Info',
        'Editace docházky jiných členů je dostupná pouze pro administrátory.',
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final (symbol, color) = _cellSymbol(cell?.status, column.isCancelled);

    return GestureDetector(
      onTap: column.isCancelled ? null : () => _onTap(context),
      onLongPress: _hasComment
          ? () => _showNote(
                context,
                'Poznámka – ${member.fullName}',
                cell!.comment!,
              )
          : null,
      child: Container(
        width: _cellWidth,
        height: _rowHeight,
        color: _rowBackground(context, row),
        padding: const EdgeInsets.all(2),
        child: Opacity(
          // Zrušený sloupec byl v MAUI jen o odstín tmavší béžová.
          opacity: column.isCancelled ? 0.5 : 1,
          child: Stack(
            children: [
              Center(
                child: Text(
                  symbol,
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                    color: color,
                  ),
                ),
              ),
              if (_hasComment)
                const Positioned(
                  top: 2,
                  right: 2,
                  child: Text('📝', style: TextStyle(fontSize: 8)),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('📋', style: TextStyle(fontSize: 48)),
          SizedBox(height: 12),
          Text(
            'V tomto měsíci nejsou žádná data',
            style: TextStyle(fontSize: 16, color: DemizonColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────
// Pomocné funkce
// ─────────────────────────────────────────────────────────────────────────

/// Symbol a barva buňky. Barvy jdou výhradně přes
/// `DemizonTheme.attendanceColor` — v MAUI byly hexy rozeseté po code-behindu.
(String, Color) _cellSymbol(String? status, bool isCancelled) {
  if (isCancelled) return ('–', DemizonTheme.attendanceColor(null));
  return switch (status) {
    'yes' => ('✓', DemizonTheme.attendanceColor('yes')),
    'maybe' => ('?', DemizonTheme.attendanceColor('maybe')),
    'no' => ('✗', DemizonTheme.attendanceColor('no')),
    _ => ('·', DemizonTheme.attendanceColor(null)),
  };
}

/// Zebra: v MAUI `row % 2 == 0` nad indexem řádku v mřížce, kde řádek 0
/// patřil hlavičce — proto se tady posouvá o jedna.
Color _rowBackground(BuildContext context, int row) {
  final isDark = Theme.of(context).brightness == Brightness.dark;
  final striped = (row + 1).isEven;
  if (striped) {
    return isDark ? DemizonColors.warmBeigeDark : DemizonColors.warmBeige;
  }
  return isDark
      ? DemizonColors.cardBackgroundDark
      : DemizonColors.cardBackground;
}

void _showNote(BuildContext context, String title, String text) {
  showDialog<void>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(title),
      content: Text(text),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('OK'),
        ),
      ],
    ),
  );
}
