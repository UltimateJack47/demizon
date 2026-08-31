import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';

/// Statistiky docházky za období — protějšek
/// `ViewModels/Attendance/AttendanceStatsViewModel.cs`.
///
/// MAUI hlídalo změnu `DateFrom`/`DateTo` přes `partial void On…Changed`
/// a ručně pouštělo `LoadCommand`. Tady je období klíčem rodiny providerů,
/// takže se načte samo.

/// Období „od–do“. Klíč rodiny, proto hodnotová rovnost; časová složka se
/// zahazuje, aby se stejný den nepovažoval za jiné období.
@immutable
class DateRange {
  DateRange(DateTime from, DateTime to)
      : from = DateTime(from.year, from.month, from.day),
        to = DateTime(to.year, to.month, to.day);

  /// Výchozí období z MAUI: od 1. ledna letošního roku po dnešek.
  factory DateRange.thisYear() {
    final today = DateTime.now();
    return DateRange(DateTime(today.year, 1, 1), today);
  }

  final DateTime from;
  final DateTime to;

  DateRange copyWith({DateTime? from, DateTime? to}) =>
      DateRange(from ?? this.from, to ?? this.to);

  @override
  bool operator ==(Object other) =>
      other is DateRange && other.from == from && other.to == to;

  @override
  int get hashCode => Object.hash(from, to);
}

final attendanceStatsProvider = AsyncNotifierProvider.family<
    AttendanceStatsController, List<MemberAttendanceStat>, DateRange>(
  AttendanceStatsController.new,
);

class AttendanceStatsController
    extends FamilyAsyncNotifier<List<MemberAttendanceStat>, DateRange> {
  @override
  Future<List<MemberAttendanceStat>> build(DateRange arg) =>
      ref.read(apiClientProvider).getAttendanceStats(arg.from, arg.to);
}

/// Souhrny z hlaviček karet. MAUI je bralo z prvního záznamu
/// (`Stats.FirstOrDefault()?.TotalRehearsals ?? 0`) — počty jsou pro všechny
/// členy stejné.
extension AttendanceStatsSummary on List<MemberAttendanceStat> {
  int get totalRehearsals => isEmpty ? 0 : first.totalRehearsals;

  int get totalActions => isEmpty ? 0 : first.totalActions;
}
