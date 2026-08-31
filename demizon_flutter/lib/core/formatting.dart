import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';

import 'api_config.dart';

/// Formátovací helpery — nahrazují `Demizon.Maui/Converters/*` a rozeseté
/// `ToString(..., CsCulture)` ve ViewModelech.
///
/// Zdroje:
///  - `Converters/DateFormatConverter.cs:13` — "dd. MMMM yyyy, HH:mm"
///  - `ViewModels/Attendance/AttendanceViewModel.cs:69` — "MMMM yyyy"
///  - `Pages/Attendance/AllMembersAttendancePage.xaml.cs:214` — "d.M."
///  - `ViewModels/EventDetailViewModel.cs:62-74` — mapování rolí CZ↔API
///  - `ViewModels/GalleryViewModel.cs:26-27` — URL obrázků

const String czechLocale = 'cs_CZ';

/// Musí se zavolat jednou před prvním formátováním (v `main.dart`, před
/// `runApp`). Bez toho `DateFormat` s locale `cs_CZ` vyhodí výjimku.
Future<void> initializeCzechFormatting() =>
    initializeDateFormatting(czechLocale);

/// Datum a čas tak, jak je zobrazoval `DateFormatConverter`:
/// `31. srpna 2026, 19:30`.
///
/// `MMMM` je v ICU tvar „v datu“ (genitiv — *srpna*), `LLLL` samostatný
/// (nominativ — *srpen*). .NET to rozlišoval přes `MonthGenitiveNames`, takže
/// tohle dělení dává stejný výsledek jako MAUI.
String formatDateTimeCz(DateTime value) =>
    DateFormat('dd. MMMM yyyy, HH:mm', czechLocale).format(value);

/// Samotné datum: `31. srpna 2026`.
String formatDateCz(DateTime value) =>
    DateFormat('dd. MMMM yyyy', czechLocale).format(value);

/// Samotný čas: `19:30`.
String formatTimeCz(DateTime value) =>
    DateFormat('HH:mm', czechLocale).format(value);

/// Popisek měsíce nad kalendářem/tabulkou: `srpen 2026`.
/// Protějšek `MonthLabel` v obou docházkových ViewModelech.
String formatMonthLabelCz(int year, int month) =>
    DateFormat('LLLL yyyy', czechLocale).format(DateTime(year, month));

/// Krátké datum do hlavičky sloupce tabulky docházky: `31.8.`
String formatShortDateCz(DateTime value) =>
    DateFormat('d.M.', czechLocale).format(value);

/// Datum pro API a pro parametry rout (`yyyy-MM-dd`), např. `rehearsalDate`
/// v MAUI (`AttendanceViewModel.cs:167`). Bez locale — je to strojový formát.
String formatApiDate(DateTime value) =>
    DateFormat('yyyy-MM-dd').format(value);

/// -------------------------------------------------------------- Role

/// Popisky role pro výběr v UI. Převzato z `EventDetailViewModel.RoleOptions`.
const List<String> roleOptions = ['Tanečník', 'Muzikant'];

/// API hodnota → český popisek. `EventDetailViewModel.cs:64-69`.
String? apiRoleToDisplay(String? apiRole) => switch (apiRole) {
      'dancer' => 'Tanečník',
      'musician' => 'Muzikant',
      _ => null,
    };

/// Český popisek → API hodnota. `EventDetailViewModel.cs:71-76`.
String? displayRoleToApi(String? displayRole) => switch (displayRole) {
      'Tanečník' => 'dancer',
      'Muzikant' => 'musician',
      _ => null,
    };

/// ----------------------------------------------------------- Obrázky

/// Obrázky se nestahují přes `ApiClient` — widget `Image.network` si je bere
/// přímo z těchto URL.
///
/// TODO(verify): ARCHITECTURE.md uvádí tvar `{baseUrl}/api/files/{id}` a
/// `/thumbnail`, jenže backend takové cesty nemá. `FilesController.cs:16`
/// vystavuje jediný endpoint `GET /api/files/{id}/image?size=full|thumb`
/// a MAUI ho tak i volalo (`GalleryViewModel.cs:26-27`,
/// `DanceDetailViewModel.cs:53-54`). Drží se tu proto backend; až se to ověří
/// proti běžícímu serveru, opravit i ARCHITECTURE.md.
String imageUrl(int fileId) => '${ApiConfig.baseUrl}/api/files/$fileId/image?size=full';

String thumbnailUrl(int fileId) =>
    '${ApiConfig.baseUrl}/api/files/$fileId/image?size=thumb';

/// URL dokumentu ke stažení. Na rozdíl od obrázků je chráněný, takže se stahuje
/// přes `ApiClient.downloadDocument` (kvůli `Authorization` hlavičce) — tahle
/// funkce je jen pro případ otevření v externím prohlížeči.
String documentUrl(int fileId) => '${ApiConfig.baseUrl}/api/files/$fileId/document';
