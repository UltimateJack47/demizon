import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import 'package:demizon/core/providers.dart';
import 'package:demizon/core/routes.dart';
import 'package:demizon/models/models.dart';

/// Měsíční docházka přihlášeného uživatele — protějšek
/// `ViewModels/Attendance/AttendanceViewModel.cs`.
///
/// MAUI drželo `CurrentYear`/`CurrentMonth` jako dvě `[ObservableProperty]`
/// a v `partial void OnCurrentYearChanged` si ručně pouštělo `LoadCommand`.
/// Tady je měsíc **argumentem rodiny** — změna měsíce = jiný provider,
/// takže načtení proběhne samo a bez ručního přepínání.

/// Rok + měsíc jako jedna hodnota. Slouží jako klíč rodiny providerů,
/// proto musí mít hodnotovou rovnost.
@immutable
class YearMonth {
  const YearMonth(this.year, this.month);

  factory YearMonth.now() {
    final today = DateTime.now();
    return YearMonth(today.year, today.month);
  }

  /// Pro router: `?year=2026&month=1`. Chybějící nebo nečitelné hodnoty
  /// znamenají aktuální měsíc (MAUI: konstruktor VM nastavil `DateTime.Today`).
  factory YearMonth.fromQuery(Map<String, String> query) {
    final year = int.tryParse(query['year'] ?? '');
    final month = int.tryParse(query['month'] ?? '');
    if (year == null || month == null || month < 1 || month > 12) {
      return YearMonth.now();
    }
    return YearMonth(year, month);
  }

  final int year;
  final int month;

  /// `if (CurrentMonth == 1) { CurrentMonth = 12; CurrentYear--; }`
  YearMonth get previous =>
      month == 1 ? YearMonth(year - 1, 12) : YearMonth(year, month - 1);

  /// `if (CurrentMonth == 12) { CurrentMonth = 1; CurrentYear++; }`
  YearMonth get next =>
      month == 12 ? YearMonth(year + 1, 1) : YearMonth(year, month + 1);

  DateTime get firstDay => DateTime(year, month, 1);

  /// Den 0 dalšího měsíce == poslední den tohoto (`DateTime.DaysInMonth`).
  DateTime get lastDay => DateTime(year, month + 1, 0);

  /// `new DateTime(...).ToString("MMMM yyyy", cs-CZ)`.
  ///
  /// Pozor na `LLLL` vs `MMMM`: .NET u formátu bez dne použije nominativ
  /// („leden“), což v `intl` odpovídá samostatnému tvaru `LLLL`.
  /// `MMMM` by dalo genitiv („ledna“).
  String get label => DateFormat('LLLL yyyy', 'cs').format(firstDay);

  @override
  bool operator ==(Object other) =>
      other is YearMonth && other.year == year && other.month == month;

  @override
  int get hashCode => Object.hash(year, month);

  @override
  String toString() => '$year-$month';
}

/// Akce a zkoušky vybraného měsíce s docházkou přihlášeného uživatele.
final attendanceProvider =
    AsyncNotifierProvider.family<AttendanceController, List<Event>, YearMonth>(
  AttendanceController.new,
);

class AttendanceController extends FamilyAsyncNotifier<List<Event>, YearMonth> {
  @override
  Future<List<Event>> build(YearMonth arg) =>
      ref.read(apiClientProvider).getEventsByMonth(arg.year, arg.month);

  /// Protějšek `RefreshCommand`. Záměrně **nenastavuje** `AsyncLoading` —
  /// stejně jako v MAUI (`AttendanceViewModel.Refresh`) zůstane starý seznam
  /// vidět a točí se jen indikátor `RefreshIndicator`u.
  Future<void> refresh() async {
    state = await AsyncValue.guard(
      () => ref.read(apiClientProvider).getEventsByMonth(arg.year, arg.month),
    );
  }
}

/// Počítadlo „X / Y“ z hlavičky. Protějšek `AttendedCount` / `TotalCount`.
extension AttendanceCounts on List<Event> {
  int get attendedCount =>
      where((e) => e.myAttendance?.status == 'yes').length;

  int get totalCount => where((e) => !e.isCancelled).length;
}

// ─────────────────────────────────────────────────────────────────────────
// Cesty
//
// MAUI skládalo Shell URI ručně (`"event-detail?rehearsalDate=…"`). Tady jsou
// stavitele cest na jednom místě, protože je potřebují dvě obrazovky
// (`attendance_screen` i `all_members_attendance_screen`).
// ─────────────────────────────────────────────────────────────────────────

final _isoDate = DateFormat('yyyy-MM-dd');

/// Detail akce.
String eventDetailPath(int eventId) => AppRoutes.eventDetailFor(eventId);

/// Detail zkoušky. Zkouška nemá `id`, nese ji datum — proto `id = 0`
/// plus query parametr, přesně jak to dělalo MAUI
/// (`EventDetailViewModel.IsRehearsal => EventId == 0 && RehearsalDateString != null`).
///
// TODO(verify): sladit s obrazovkou detailu akce, až vznikne — očekává se, že
// route `/events/:id` přečte `rehearsalDate` z query a při `id == 0` se
// přepne do režimu zkoušky.
String rehearsalDetailPath(DateTime date) =>
    '${AppRoutes.eventDetailFor(0)}?rehearsalDate=${_isoDate.format(date)}';

/// Admin: docházka konkrétního člena na konkrétní **akci**.
String memberAttendanceDetailPath({
  required int eventId,
  required int memberId,
  required String memberName,
}) =>
    '${AppRoutes.memberAttendanceDetail}'
    '?eventId=$eventId'
    '&memberId=$memberId'
    '&memberName=${Uri.encodeComponent(memberName)}';

/// Admin: docházka konkrétního člena na konkrétní **zkoušce**.
String memberRehearsalDetailPath({
  required DateTime date,
  required int memberId,
  required String memberName,
}) =>
    '${AppRoutes.memberAttendanceDetail}'
    '?rehearsalDate=${_isoDate.format(date)}'
    '&memberId=$memberId'
    '&memberName=${Uri.encodeComponent(memberName)}';

/// Přehled docházky všech členů pro daný měsíc.
String attendanceOverviewPath(YearMonth month) =>
    '${AppRoutes.attendanceOverview}?year=${month.year}&month=${month.month}';

/// Statistiky za celý zvolený měsíc (MAUI: `from` = 1. den, `to` = poslední).
String attendanceStatsPath(YearMonth month) =>
    '${AppRoutes.attendanceStats}'
    '?from=${_isoDate.format(month.firstDay)}'
    '&to=${_isoDate.format(month.lastDay)}';
