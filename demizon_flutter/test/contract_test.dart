import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/features/attendance/attendance_controller.dart';
import 'package:demizon/features/attendance/member_attendance_detail_controller.dart';
import 'package:demizon/features/events/event_detail_controller.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';

/// Testy kontraktu mezi aplikací a API.
///
/// Přepis z MAUI mohl tyhle věci snadno rozbít, protože jde o holé řetězce,
/// které kompilátor nehlídá — status docházky, hodnoty rolí a formát data
/// v query parametrech.
void main() {
  setUpAll(() async => initializeDateFormatting(czechLocale));

  group('status docházky', () {
    test('mapuje se na barvy podle kontraktu API', () {
      expect(DemizonTheme.attendanceColor('yes'), DemizonColors.attendanceYes);
      expect(
        DemizonTheme.attendanceColor('maybe'),
        DemizonColors.attendanceMaybe,
      );
      expect(DemizonTheme.attendanceColor('no'), DemizonColors.attendanceNo);
    });

    test('neznámý status a null padají na neutrální barvu', () {
      expect(DemizonTheme.attendanceColor(null), DemizonColors.attendanceNone);
      expect(DemizonTheme.attendanceColor('Yes'), DemizonColors.attendanceNone);
      expect(DemizonTheme.attendanceColor(''), DemizonColors.attendanceNone);
    });
  });

  group('role', () {
    test('převod tam a zpět je konzistentní', () {
      for (final display in roleOptions) {
        expect(apiRoleToDisplay(displayRoleToApi(display)), display);
      }
    });

    test('API hodnoty odpovídají EventDetailViewModel.cs:62-74', () {
      expect(displayRoleToApi('Tanečník'), 'dancer');
      expect(displayRoleToApi('Muzikant'), 'musician');
      expect(apiRoleToDisplay('dancer'), 'Tanečník');
      expect(apiRoleToDisplay('musician'), 'Muzikant');
    });

    test('neznámá role je null, ne výjimka', () {
      expect(apiRoleToDisplay(null), isNull);
      expect(apiRoleToDisplay('singer'), isNull);
      expect(displayRoleToApi('Zpěvák'), isNull);
    });
  });

  group('datum pro API', () {
    test('formátuje se jako yyyy-MM-dd bez ohledu na locale', () {
      expect(formatApiDate(DateTime(2026, 1, 2)), '2026-01-02');
      expect(formatApiDate(DateTime(2026, 12, 31)), '2026-12-31');
    });

    test('endpointy zkoušek dostanou datum, ne čas', () {
      expect(formatApiDate(DateTime(2026, 5, 15, 18, 30)), '2026-05-15');
    });

    test('query interceptor posílá yyyy-MM-dd, ne ISO instant', () {
      expect(encodeQueryValue(DateTime(2026, 5, 15, 18, 30)), '2026-05-15');
      expect(encodeQueryValue('2026-05-15T00:00:00.000'), '2026-05-15');
      expect(encodeQueryValue('2026-05-15T22:00:00.000Z'), '2026-05-15');
      expect(encodeQueryValue('2026-05-15'), '2026-05-15');
      expect(encodeQueryValue(12), 12);
    });

    test('cesta ke zkoušce nese datum, ne čas', () {
      expect(
        rehearsalDetailPath(DateTime(2026, 5, 15, 18, 30)),
        '/events/0?rehearsalDate=2026-05-15',
      );
    });
  });

  group('duální režim akce / zkouška', () {
    test('fromRoute: id 0 + datum je zkouška, kladné id je akce', () {
      final rehearsal = EventDetailArgs.fromRoute(
        eventId: 0,
        rehearsalDate: DateTime(2026, 5, 15),
      );
      expect(rehearsal.isRehearsal, isTrue);
      expect(rehearsal.eventId, isNull);

      final event = EventDetailArgs.fromRoute(eventId: 12);
      expect(event.isRehearsal, isFalse);
      expect(event.eventId, 12);
    });

    test('MemberAttendanceTarget pozná zkoušku i z eventId 0', () {
      final rehearsal = MemberAttendanceTarget(
        eventId: 0,
        rehearsalDate: DateTime(2026, 1, 16),
        memberId: 3,
        memberName: 'Jan',
      );
      expect(rehearsal.isRehearsal, isTrue);

      const event = MemberAttendanceTarget(
        eventId: 12,
        memberId: 3,
        memberName: 'Jan',
      );
      expect(event.isRehearsal, isFalse);
    });
  });

  group('URL souborů', () {
    test('odpovídají FilesController.cs:16', () {
      expect(imageUrl(42), endsWith('/api/files/42/image?size=full'));
      expect(thumbnailUrl(42), endsWith('/api/files/42/image?size=thumb'));
      expect(documentUrl(42), endsWith('/api/files/42/document'));
    });
  });

  group('zrušená zkouška', () {
    Map<String, dynamic> rehearsalJson({
      required bool isCancelled,
      String? reason,
      String date = '2026-06-12',
    }) =>
        {
          'id': 0,
          'name': 'Zkouška',
          'dateFrom': '${date}T19:00:00',
          'dateTo': '${date}T21:30:00',
          'place': null,
          'isCancelled': isCancelled,
          'recurrence': 'none',
          'myAttendance': null,
          'isRehearsal': true,
          'isPublic': false,
          'notifyBeforeDays': null,
          if (reason != null) 'cancellationReason': reason,
        };

    test('důvod přijde v EventDto i v MonthlyColumnDto', () {
      final event =
          Event.fromJson(rehearsalJson(isCancelled: true, reason: 'Hody'));
      expect(event.isCancelled, isTrue);
      expect(event.cancellationReason, 'Hody');

      final column = MonthlyColumn.fromJson({
        'eventId': null,
        'label': 'Zkouška 12.6.',
        'date': '2026-06-12T00:00:00',
        'isEvent': false,
        'isCancelled': true,
        'cancellationReason': 'Hody',
      });
      expect(column.cancellationReason, 'Hody');
    });

    test('starší server bez důvodu se načte s null', () {
      final event = Event.fromJson(rehearsalJson(isCancelled: false));
      expect(event.cancellationReason, isNull);
    });

    test('detail najde zkoušku v měsíci podle dne, ne akci', () {
      final month = [
        Event.fromJson({
          ...rehearsalJson(isCancelled: false, date: '2026-06-05'),
          'id': 7,
          'name': 'Vystoupení',
          'isRehearsal': false,
        }),
        Event.fromJson(rehearsalJson(isCancelled: true, reason: 'Hody')),
      ];

      expect(
        findRehearsal(month, DateTime(2026, 6, 12))?.cancellationReason,
        'Hody',
      );
      expect(findRehearsal(month, DateTime(2026, 6, 5)), isNull);
    });

    test('zrušená zkouška a den bez zkoušky jsou zamčené, záloha ne', () {
      final day = DateTime(2026, 6, 12);
      final args = EventDetailArgs(rehearsalDate: day);
      final cancelled = Event.fromJson(rehearsalJson(isCancelled: true));

      expect(EventDetailState(args: args, event: cancelled).isLocked, isTrue);
      expect(
        EventDetailState(args: args, event: noRehearsalOn(day)).isLocked,
        isTrue,
      );
      expect(
        EventDetailState(args: args, event: rehearsalPlaceholder(day)).isLocked,
        isFalse,
      );
    });

    test('záloha má čas zkoušky, ne půlnoc', () {
      final placeholder = rehearsalPlaceholder(DateTime(2026, 6, 12));
      expect(placeholder.dateFrom, DateTime(2026, 6, 12, 19));
      expect(placeholder.dateTo, DateTime(2026, 6, 12, 21, 30));
    });
  });
}
