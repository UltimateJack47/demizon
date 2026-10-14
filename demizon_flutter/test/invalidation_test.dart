import 'package:demizon/api/api_client.dart';
import 'package:demizon/core/invalidation.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/features/attendance/attendance_controller.dart';
import 'package:demizon/features/events/events_controller.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Počítá jen volání, která čtou testované providery. Zbytek API tu není potřeba.
class _CountingApi implements ApiClient {
  int upcoming = 0;
  final monthly = <String, int>{};

  @override
  Future<List<Event>> getUpcomingEvents() async {
    upcoming++;
    return const [];
  }

  @override
  Future<List<Event>> getEventsByMonth(int year, int month) async {
    monthly.update('$year-$month', (n) => n + 1, ifAbsent: () => 1);
    return const [];
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

/// Regrese: uložení docházky dřív invalidovalo jen seznam akcí. Měsíční
/// přehled zůstal z cache, a to i po přepnutí měsíce tam a zpátky, protože
/// rodina providerů drží každý načtený měsíc.
void main() {
  late _CountingApi api;
  late ProviderContainer container;

  setUp(() {
    api = _CountingApi();
    container = ProviderContainer(
      overrides: [apiClientProvider.overrideWithValue(api)],
    );
  });

  tearDown(() => container.dispose());

  test('změna docházky znovu načte akce i všechny načtené měsíce', () async {
    // Drží providery naživu jako otevřené obrazovky.
    container.listen(eventsProvider, (_, __) {});
    container.listen(attendanceProvider(const YearMonth(2026, 10)), (_, __) {});
    await container.read(eventsProvider.future);
    await container.read(attendanceProvider(const YearMonth(2026, 10)).future);
    await container.read(attendanceProvider(const YearMonth(2026, 11)).future);

    invalidateAttendanceViews(container.invalidate);

    await container.read(eventsProvider.future);
    await container.read(attendanceProvider(const YearMonth(2026, 10)).future);
    await container.read(attendanceProvider(const YearMonth(2026, 11)).future);
    expect(api.upcoming, 2);
    expect(api.monthly, {'2026-10': 2, '2026-11': 2});
  });

  test('nová session zahodí data minulého uživatele', () async {
    await container.read(eventsProvider.future);
    await container.read(attendanceProvider(const YearMonth(2026, 10)).future);

    invalidateSessionData(container.invalidate);

    await container.read(eventsProvider.future);
    await container.read(attendanceProvider(const YearMonth(2026, 10)).future);
    expect(api.upcoming, 2);
    expect(api.monthly, {'2026-10': 2});
  });
}
