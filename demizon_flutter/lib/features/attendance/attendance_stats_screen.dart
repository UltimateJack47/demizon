import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'attendance_stats_controller.dart';

final _dateFormat = DateFormat('dd.MM.yyyy');

/// Přepis `Pages/Attendance/AttendanceStatsPage.xaml` +
/// `AttendanceStatsViewModel.cs`.
///
/// Statistiky docházky za zvolené období: souhrn zkoušek a akcí plus
/// seznam členů s procenty a progress bary.
class AttendanceStatsScreen extends ConsumerStatefulWidget {
  const AttendanceStatsScreen({super.key, this.initialRange});

  /// MAUI: query parametry `from` / `to` (posílá je hlavní obrazovka docházky
  /// za právě zobrazený měsíc).
  final DateRange? initialRange;

  @override
  ConsumerState<AttendanceStatsScreen> createState() =>
      _AttendanceStatsScreenState();
}

class _AttendanceStatsScreenState extends ConsumerState<AttendanceStatsScreen> {
  late DateRange _range = widget.initialRange ?? DateRange.thisYear();

  /// `MaxDate => DateTime.Today`
  DateTime get _today {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  Future<void> _pickFrom() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _range.from,
      firstDate: DateTime(2000),
      // MAUI: MaximumDate="{Binding DateTo}"
      lastDate: _range.to,
    );
    if (picked != null) setState(() => _range = _range.copyWith(from: picked));
  }

  Future<void> _pickTo() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _range.to,
      // MAUI: MinimumDate="{Binding DateFrom}", MaximumDate="{Binding MaxDate}"
      firstDate: _range.from,
      // MAUI mělo natvrdo `MaxDate => Today`, jenže hlavní obrazovka posílá
      // jako `to` poslední den zobrazeného měsíce — u aktuálního měsíce tedy
      // datum v budoucnu. `showDatePicker` by na tom spadl na assertu, proto
      // se strop roztahuje na už zvolené datum.
      lastDate: _range.to.isAfter(_today) ? _range.to : _today,
    );
    if (picked != null) setState(() => _range = _range.copyWith(to: picked));
  }

  @override
  Widget build(BuildContext context) {
    final stats = ref.watch(attendanceStatsProvider(_range));
    final items = stats.valueOrNull ?? const <MemberAttendanceStat>[];

    return Scaffold(
      appBar: AppBar(title: const Text('Statistiky docházky')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _rangeCard(),
          const SizedBox(height: 16),
          if (items.isNotEmpty) ...[
            Row(
              children: [
                Expanded(
                  child: _SummaryCard(
                    emoji: '🎵',
                    value: items.totalRehearsals,
                    caption: 'Zkoušek',
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _SummaryCard(
                    emoji: '🎭',
                    value: items.totalActions,
                    caption: 'Akcí',
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
          ],
          if (stats.isLoading)
            const Center(child: CircularProgressIndicator())
          else if (stats.hasError)
            const Center(
              child: Text(
                'Nepodařilo se načíst statistiky.',
                style: TextStyle(color: DemizonColors.error, fontSize: 14),
              ),
            )
          else if (items.isEmpty)
            const Center(
              child: Text(
                'Žádná data pro zvolené období.',
                style:
                    TextStyle(fontSize: 16, color: DemizonColors.textSecondary),
              ),
            )
          else ...[
            const Text(
              'Přehled členů',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
            ),
            const SizedBox(height: 8),
            for (final stat in items) _MemberStatCard(stat: stat),
          ],
          const SizedBox(height: 20),
        ],
      ),
    );
  }

  Widget _rangeCard() {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Období',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: _DateField(
                    date: _range.from,
                    onTap: _pickFrom,
                  ),
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 8),
                  child: Text('–', style: TextStyle(fontSize: 18)),
                ),
                Expanded(
                  child: _DateField(
                    date: _range.to,
                    onTap: _pickTo,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            FilledButton(
              // Data se načtou už při změně data (jako `OnDateFromChanged`
              // v MAUI); tohle je explicitní znovunačtení.
              onPressed: () =>
                  ref.invalidate(attendanceStatsProvider(_range)),
              child: const Text('Zobrazit'),
            ),
          ],
        ),
      ),
    );
  }
}

class _DateField extends StatelessWidget {
  const _DateField({required this.date, required this.onTap});

  final DateTime date;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: InputDecorator(
        decoration: const InputDecoration(
          isDense: true,
          contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
        ),
        child: Text(_dateFormat.format(date)),
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.emoji,
    required this.value,
    required this.caption,
  });

  final String emoji;
  final int value;
  final String caption;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Text(emoji, style: const TextStyle(fontSize: 24)),
            const SizedBox(height: 4),
            Text(
              '$value',
              style: const TextStyle(
                fontSize: 28,
                fontWeight: FontWeight.bold,
                color: DemizonColors.primary,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              caption,
              style: const TextStyle(
                fontSize: 12,
                color: DemizonColors.textSecondary,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MemberStatCard extends StatelessWidget {
  const _MemberStatCard({required this.stat});

  final MemberAttendanceStat stat;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              stat.fullName,
              style:
                  const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
            ),
            const SizedBox(height: 8),
            _RateRow(
              label: 'Zkoušky',
              attended: stat.attendedRehearsals,
              total: stat.totalRehearsals,
              rate: stat.rehearsalRate,
            ),
            const SizedBox(height: 8),
            _RateRow(
              label: 'Akce',
              attended: stat.attendedActions,
              total: stat.totalActions,
              rate: stat.actionRate,
            ),
          ],
        ),
      ),
    );
  }
}

class _RateRow extends StatelessWidget {
  const _RateRow({
    required this.label,
    required this.attended,
    required this.total,
    required this.rate,
  });

  final String label;
  final int attended;
  final int total;

  /// Procenta 0–100, jak je posílá API (`RehearsalRate` / `ActionRate`).
  final double rate;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        SizedBox(
          width: 60,
          child: Text(
            label,
            style: const TextStyle(
              fontSize: 12,
              color: DemizonColors.textSecondary,
            ),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: LinearProgressIndicator(
            // Protějšek RateToProgressConverter: Math.Clamp(rate / 100, 0, 1)
            value: (rate / 100).clamp(0.0, 1.0),
            color: _rateColor(rate),
            backgroundColor: DemizonColors.attendanceNone,
          ),
        ),
        const SizedBox(width: 8),
        Text.rich(
          TextSpan(
            children: [
              TextSpan(
                text: '$attended',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const TextSpan(text: '/'),
              TextSpan(text: '$total'),
              const TextSpan(text: ' ('),
              TextSpan(
                text: '${rate.round()}%',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const TextSpan(text: ')'),
            ],
          ),
          style: const TextStyle(fontSize: 13),
        ),
      ],
    );
  }
}

/// Protějšek `AttendanceRateColorConverter`: ≥80 zelená, ≥50 žlutá, jinak
/// červená. Barvy se berou z palety stavů docházky — jsou to tytéž hodnoty.
Color _rateColor(double rate) {
  if (rate >= 80) return DemizonTheme.attendanceColor('yes');
  if (rate >= 50) return DemizonTheme.attendanceColor('maybe');
  return DemizonTheme.attendanceColor('no');
}
