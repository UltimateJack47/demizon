import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/theme.dart';
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
  });

  group('URL souborů', () {
    test('odpovídají FilesController.cs:16', () {
      expect(imageUrl(42), endsWith('/api/files/42/image?size=full'));
      expect(thumbnailUrl(42), endsWith('/api/files/42/image?size=thumb'));
      expect(documentUrl(42), endsWith('/api/files/42/document'));
    });
  });
}
