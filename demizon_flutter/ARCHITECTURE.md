# Demizon Flutter — architektura a konvence

> **Závazný kontrakt.** Tento soubor si přečti dřív, než napíšeš jakýkoli kód.
> Cílem je **1:1 přepis** `Demizon.Maui` — stejné obrazovky, stejné chování,
> stejné texty. Ne redesign.

## Zásady

1. **Backend se nemění.** API kontrakt je daný `Demizon.Mvc/Controllers/Api/*`
   a DTO v `Demizon.Contracts`. Nic nevymýšlej, nic nepřidávej.
2. **Texty v UI jsou česky**, přesně jak jsou v MAUI verzi. Kopíruj je z odpovídající
   `.xaml` stránky.
3. **Neportuj workaroundy.** MAUI kód obsahuje ~700 řádků obcházení frameworku
   (`LongPressBehavior`, `SwipeGestureInterceptor`, skrývání toolbaru pětinásobným
   pollingem, ruční window insets). Tohle všechno ve Flutteru řeší framework —
   `GestureDetector`, `SafeArea`, vlastnosti widgetů. Portuj **záměr**, ne řešení.
4. **Žádný `print`.** Chyby propaguj, nebo zobraz uživateli přes `SnackBar`.

## Struktura

```
lib/
  main.dart                 — bootstrap, ProviderScope
  app.dart                  — MaterialApp.router, téma, router
  core/
    api_config.dart         — base URL          [HOTOVO]
    theme.dart              — paleta + ThemeData [HOTOVO]
    routes.dart             — cesty              [HOTOVO]
    auth/
      token_storage.dart    — protějšek TokenStorage.cs
      auth_interceptor.dart — protějšek AuthHandler.cs
      auth_controller.dart  — přihlášení/odhlášení, stav session
    formatting.dart         — datum/čas helpery, mapování rolí
  models/                   — Dart protějšky Demizon.Contracts
  api/
    api_client.dart         — Retrofit rozhraní (protějšek IApiClient.cs)
  features/
    <oblast>/
      <nazev>_screen.dart   — widget obrazovky
      <nazev>_controller.dart — Riverpod provider se stavem
```

## Technologie

| Vrstva | MAUI | Flutter |
|---|---|---|
| Stav | CommunityToolkit.Mvvm (`[ObservableProperty]`) | **Riverpod** (`AsyncNotifier` / `Notifier`) |
| Routing | Shell + `Routing.RegisterRoute` | **go_router** |
| HTTP | Refit + `HttpClient` | **dio + retrofit** |
| Serializace | System.Text.Json | **json_serializable** |
| Bezpečné úložiště | `SecureStorage` | **flutter_secure_storage** |
| Preference | `Preferences` | **shared_preferences** |
| Push | Plugin.Firebase.CloudMessaging | **firebase_messaging** |
| Lokální notifikace | ruční `NotificationCompat` v `MainActivity.cs` | **flutter_local_notifications** |
| Tap + long-press | `LongPressBehavior` (158 ř. + 600ms guard) | **`GestureDetector(onTap:, onLongPress:)`** |
| Swipe | `SwipeGestureInterceptor` (104 ř. + hook v Activity) | **`onHorizontalDragEnd`** |

## Konvence modelů

Soubor `lib/models/<snake_case>.dart`, třída v PascalCase bez sufixu `Dto`.
Každý model má `fromJson`/`toJson` přes `json_serializable`, immutable (`final` pole,
`const` konstruktor).

**Pozor na JSON klíče:** ASP.NET Core serializuje camelCase, C# recordy jsou PascalCase.
Používej `@JsonSerializable()` bez `fieldRename` a pojmenuj Dart pole camelCase —
tím to sedí automaticky.

| C# (Demizon.Contracts) | Dart (lib/models/) |
|---|---|
| `AttendanceDto` | `Attendance` — `attendance.dart` |
| `UpsertAttendanceRequest` | `UpsertAttendanceRequest` — `upsert_attendance_request.dart` |
| `MemberAttendanceStatDto` | `MemberAttendanceStat` — `member_attendance_stat.dart` |
| `MemberCellDto` | `MemberCell` — `member_cell.dart` |
| `MemberMonthlyRowDto` | `MemberMonthlyRow` — `member_monthly_row.dart` |
| `MonthlyAttendanceTableDto` | `MonthlyAttendanceTable` — `monthly_attendance_table.dart` |
| `MonthlyColumnDto` | `MonthlyColumn` — `monthly_column.dart` |
| `TokenRequest` / `TokenResponse` / `RefreshRequest` | stejně — `auth.dart` (všechny tři v jednom souboru) |
| `DanceDto` | `Dance` — `dance.dart` |
| `DanceDocumentDto` | `DanceDocument` — `dance_document.dart` |
| `VideoLinkDto` | `VideoLink` — `video_link.dart` |
| `CreateVideoLinkRequest` | `CreateVideoLinkRequest` — `create_video_link_request.dart` |
| `EventDto` | `Event` — `event.dart` |
| `CreateEventRequest` / `UpdateEventRequest` | stejně — `event_requests.dart` |
| `EventAttendeeDto` / `EventAttendeesDto` | `EventAttendee` / `EventAttendees` — `event_attendees.dart` |
| `GalleryPhotoDto` | `GalleryPhoto` — `gallery_photo.dart` |
| `MemberProfileDto` | `MemberProfile` — `member_profile.dart` |
| `UpdateProfileRequest` / `ChangePasswordRequest` | stejně — `member_requests.dart` |
| `RegisterDeviceRequest` | stejně — `register_device_request.dart` |
| `NotifyMissingAttendanceResponse` | `NotifyMissingAttendanceResponse` — `notify_missing_attendance_response.dart` |

`lib/models/models.dart` je barrel soubor, který reexportuje všechny modely.

## Kontrakt API klienta

`lib/api/api_client.dart`, třída `ApiClient` s anotací `@RestApi()`.
Názvy metod = názvy z `IApiClient.cs` **bez sufixu `Async`** a v camelCase:
`LoginAsync` → `login`, `GetUpcomingEventsAsync` → `getUpcomingEvents`.

**Nepřenášet** (v MAUI nemají volajícího — ověřeno):
`GetVideosAsync`, `GetVideoAsync`, `CreateVideoAsync`, `UpdateVideoAsync`,
`DeleteVideoAsync`, `ToggleEventCancelledAsync`, `ToggleEventPublicAsync`.
Zbývá **35 živých endpointů**.

Obrázky se nestahují přes klienta — jde o přímé URL:
`{baseUrl}/api/files/{id}/thumbnail` a `{baseUrl}/api/files/{id}`.

## Kontrakt statusů docházky

Řetězce **`"yes"` | `"maybe"` | `"no"`**, malými písmeny. Nikdy neposílej nic jiného.
Barvy k nim dává `DemizonTheme.attendanceColor(status)`.

Role (`activityRole`) se v MAUI mapovala mezi českým popiskem a API hodnotou
(`EventDetailViewModel.cs:62-74`) — tuto mapu přenes do `lib/core/formatting.dart`.

## Zkoušky vs. akce

Zásadní doména, kterou je nutné pochopit: **zkoušky nejsou události.** Jsou to
řádky docházky s `eventId == null` a páteční datum. Proto má API zvlášť
`/api/attendances/rehearsal` s parametrem `date`. Obrazovky, které zobrazují
obojí dohromady, musí tento dvojí režim řešit — v MAUI to nese
`EventDetailViewModel.IsRehearsal` (`:22`).

## Riverpod vzor

```dart
final eventsProvider = AsyncNotifierProvider<EventsController, List<Event>>(
  EventsController.new,
);

class EventsController extends AsyncNotifier<List<Event>> {
  @override
  Future<List<Event>> build() => ref.read(apiClientProvider).getUpcomingEvents();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(apiClientProvider).getUpcomingEvents(),
    );
  }
}
```

Obrazovka je `ConsumerWidget` a stav řeší přes `.when(data:, loading:, error:)`.

## Stav prací

Průběžný stav a co zbývá je v [`../docs/flutter-rewrite-plan.md`](../docs/flutter-rewrite-plan.md).

## Ověření

Flutter SDK zatím **není nainstalované** na vývojovém stroji, takže kód není
zkompilovaný ani spuštěný. Až bude, první kroky jsou:

```
flutter pub get
dart run build_runner build --delete-conflicting-outputs
flutter analyze
```

Do té doby ke každému nedořešenému místu piš `// TODO(verify):` s popisem,
co je potřeba ověřit.
