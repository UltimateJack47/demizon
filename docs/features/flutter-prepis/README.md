# Přepis mobilní aplikace z .NET MAUI do Flutteru

> **Živý dokument.** Průběžně aktualizovat.
> Poslední aktualizace: 2026-09-09 (checklist na telefon + Firebase).
> Založeno: 2026-09-01. Větev: `feat/flutter-app`. Adresář: `demizon_flutter/`.
>
> Pořadí vůči backendu a nasazení: [`STATUS.md`](../../STATUS.md).

## Proč

MAUI verze má měřitelně vyšší chybovost než backend: **43 commitů / 15 oprav (35 %)**
proti **155 commitů / 16 oprav (10 %)** u backendu. Všech 15 oprav kolem gest, NavBaru
a status baru padlo do **jediného dne, 2026-04-23**.

V kódu je **~700 řádků čistého obcházení frameworku** bez business hodnoty:

| Workaround | Řádků | Ve Flutteru |
|---|---:|---|
| `Behaviors/LongPressBehavior.cs` + 6 guard checků, 600ms okno | 158 | `GestureDetector(onTap:, onLongPress:)` |
| `Platforms/Android/SwipeGestureInterceptor.cs` + hook v Activity | 104 | `onHorizontalDragEnd` (~5 ř.) |
| Prázdný code-behind boilerplate (15 z 16 souborů) | ~305 | zaniká |
| Skrývání Android toolbaru pětinásobným pollingem (`AppShell.xaml.cs:41-46`) | 45 | vlastnost widgetu |
| Ruční window insets pro Android 15 (`MainActivity.cs:142-158`) | ~40 | `SafeArea` |
| Ruční DI + route registrace (trojí synchronizace) | 43 | router na jednom místě |

**Hlavní argument ale je jiný:** `Demizon.Maui.csproj:4` má
`<TargetFrameworks>net10.0-android</TargetFrameworks>` a `Platforms/iOS/AppDelegate.cs`
je celý zakomentovaný. Dnešní pokrytí iOS je **nula procent**. Flutterem přijde v ceně.

Dva vedlejší přínosy zdarma: `App.xaml.cs:25-26` má natvrdo `AppTheme.Light`, protože
dark mode nebyl dodělaný — ve Flutteru je téma kompletní pro obě varianty. A prohlížeč
fotek dostane pinch-to-zoom, který MAUI verze neměla.

## Rozsah

Zdroj: **6 409 řádků** (4 145 C# + 2 264 XAML), 16 obrazovek, 17 ViewModelů,
42 endpointů v `IApiClient.cs` (z toho 7 mrtvých → přenáší se 35).

Cíl: odhad **3 500–4 500 řádků Dart**. Deklarativní UI je hutnější než XAML,
ale controllery bez source-genu jsou upovídanější.

## Zásada

**1:1 přepis.** Stejné obrazovky, stejné chování, stejné české texty.
Ne redesign. Backend se nemění — API kontrakt zůstává.

Závazné konvence jsou v [`../demizon_flutter/ARCHITECTURE.md`](../../../demizon_flutter/ARCHITECTURE.md).

## Technologické mapování

| Vrstva | MAUI | Flutter |
|---|---|---|
| Stav | CommunityToolkit.Mvvm | Riverpod |
| Routing | Shell + `Routing.RegisterRoute` | go_router |
| HTTP | Refit | dio + retrofit |
| Serializace | System.Text.Json | json_serializable |
| Bezpečné úložiště | `SecureStorage` | flutter_secure_storage |
| Preference | `Preferences` | shared_preferences |
| Push | Plugin.Firebase.CloudMessaging | firebase_messaging |
| Lokální notifikace | ruční `NotificationCompat` v `MainActivity.cs:79-108` | flutter_local_notifications |

## Stav

### ✅ Základ

| Soubor | Obsah |
|---|---|
| `pubspec.yaml` | závislosti |
| `ARCHITECTURE.md` | závazný kontrakt pro celý přepis |
| `lib/core/api_config.dart` | base URL, přebíjitelná přes `--dart-define` |
| `lib/core/theme.dart` | paleta 1:1 z `Colors.xaml`, Material 3, light + dark |
| `lib/core/routes.dart` | cesty — bez MAUI omezení na ploché názvy bez `/` |
| `lib/core/router.dart` | go_router: login + 4 taby (`StatefulShellRoute`) + galerie |
| `lib/main.dart` | bootstrap, Firebase, `initializeDateFormatting('cs_CZ')` |
| `lib/app.dart` | `MaterialApp.router`, `ThemeMode.system`, locale cs_CZ |
| `lib/features/shell/main_shell.dart` | spodní navigace, 4 taby |

### ✅ Modely — 20 souborů + barrel

Všech 24 DTO z `Demizon.Contracts` přepsáno do `lib/models/`.
Immutable, pojmenované parametry, `json_serializable`.

Poznámky:
- `explicitToJson: true` u pěti tříd s vnořenými objekty (`Event`, `Dance`,
  `MemberMonthlyRow`, `MonthlyAttendanceTable`, `EventAttendees`) — bez toho by
  `toJson()` vracel instanci místo `Map`.
- **Ztratila se value equality.** C# `record` má strukturální rovnost, obyčejná
  Dart třída referenční. Kdyby se objevily zbytečné rebuildy v Riverpodu,
  řešením je `equatable` nebo `freezed`.
- `DateTime` přichází ze serveru s `Kind=Unspecified`; `DateTime.parse` to bere
  jako lokální čas. Pro jedno pásmo správně, ale je to označené `// TODO(verify):`.

### ✅ API klient a auth

- `lib/api/api_client.dart` — **35 živých endpointů** (7 mrtvých vynecháno), retrofit.
- `lib/core/auth/token_storage.dart` — 7 klíčů v secure storage, in-memory cache expirace.
- `lib/core/auth/auth_interceptor.dart` — proaktivní refresh 5 min před expirací
  + fallback na 401, souběh přes jeden sdílený `Future`.
  Refresh jde přes **samostatnou Dio instanci bez interceptoru**, takže odpadá
  obdoba `TokenRefreshHelper.cs:36` (v MAUI existoval jen kvůli cyklické DI závislosti).
- `lib/core/auth/auth_controller.dart`, `lib/core/providers.dart`, `lib/core/formatting.dart`.

### ✅ Všech 16 obrazovek

| Oblast | Obrazovky |
|---|---|
| auth | login |
| events | seznam, detail (duální režim akce/zkouška), založení, editace |
| attendance | měsíční přehled, křížová tabulka všech členů, statistiky, editace docházky člena |
| dances | seznam s filtrem, detail (videa / fotky / dokumenty) |
| gallery | mřížka, prohlížeč (+ pinch-to-zoom, vylepšení proti MAUI) |
| profile | profil, editace, změna hesla |

### Rozsah k 2026-09-01

**61 souborů, 8 049 řádků Dart** (modely 703, API 190, core 1 023, obrazovky 6 077).
Proti 6 409 řádkům C#+XAML v MAUI. Číslo je vyšší než odhadovaných 3 500–4 500,
protože kód nese hustou dokumentaci s odkazy na zdrojové MAUI soubory — ta při
ověřování a dolaďování ušetří čas.

### Provedené kontroly (bez SDK)

Kód nejde zkompilovat, ale statická kontrola proběhla a je čistá:
- všechny relativní i `package:` importy ukazují na existující soubory,
- všechny třídy volané z `router.dart` jsou definované,
- pojmenované parametry v konstruktorech obrazovek sedí na volání z routeru.

Jedna skutečná trhlina z paralelní práce se tím našla a opravila: router očekával
`MemberAttendanceArgs`, obrazovka bere `MemberAttendanceTarget`.

## ✅ Zkompilováno a postaveno

Flutter **3.47.2 / Dart 3.13.2** doinstalován 2026-09-01. Stav:

| Krok | Výsledek |
|---|---|
| `flutter pub get` | OK |
| `dart run build_runner build` | **42 vygenerovaných souborů** |
| `flutter analyze` | **No issues found!** |
| `flutter test` | **26 testů prošlo** (2026-09-09; bylo 14) |
| `flutter build apk --debug` | **APK postaveno** (159 MB debug) |
| **spuštěno na emulátoru** | **Pixel 9 / API 36 — aplikace běží** |

Obavy z integračních chyb po paralelní práci pěti agentů se nenaplnily — analyzér
našel jen **3 nálezy** na 61 souborech a žádnou skutečnou chybu. Zásluhu na tom má
`ARCHITECTURE.md` napsaný před spuštěním agentů.

### Co bylo potřeba opravit

| Problém | Oprava |
|---|---|
| `intl ^0.19.0` × `flutter_localizations` chce `^0.20.3` | bump |
| `retrofit 4.10.0` × `retrofit_generator 9.7.0` — generátor neznal `Parser.DartMappable` | generátor na `^10.2.10` |
| `json_annotation` a SDK constraint pod požadovaným minimem | `^4.12.0`, `sdk: ^3.8.0` |
| `return _authenticatedFromStorage()` bez `await` uvnitř `try` | **skutečná chyba** — výjimka by utekla mimo `catch` a obnova session by spadla místo tichého návratu na přihlášení |
| `Switch.activeColor` deprecated | `activeThumbColor` |
| `flutter_local_notifications` vyžaduje core library desugaring | `isCoreLibraryDesugaringEnabled` + `desugar_jdk_libs` v `android/app/build.gradle.kts` |
| `flutter create` přidal boilerplate test na neexistující `MyApp` | nahrazen `test/contract_test.dart` |

### Testy

`flutter test` — **26 testů** (2026-09-09):

- `test/contract_test.dart` — kontrakt s API, který kompilátor nehlídá: statusy
  docházky (`yes`/`maybe`/`no`), mapování rolí, `yyyy-MM-dd` na drátě u zkoušek,
  duální režim akce/zkouška, tvar URL souborů.
- `test/auth_state_test.dart` — `isRestoring` vs. přihlašování (router nesmí
  zahodit login obrazovku).
- `test/notification_navigation_test.dart` — parse payloadu, pending replay
  po loginu, reset po odhlášení.

### Ověřeno za běhu na emulátoru

Aplikace nastartuje, vykreslí přihlašovací obrazovku včetně loga a české
diakritiky, a korektně zobrazí chybu při neúspěšném přihlášení.

Firebase při startu selže (`Failed to load FirebaseOptions from resource`),
protože chybí `google-services.json` — `try/catch` v `main.dart` to ale zachytí
a aplikace pokračuje. To je zatím v pořádku; notifikace stejně nejsou dopsané.

**Dvě chyby, které odhalilo teprve spuštění** (statická analýza je najít nemohla):

1. **Hero pruh na přihlašovací obrazovce zabíral jen část šířky.**
   `Container` v `_Hero` měl `height: 320`, ale žádnou šířku. Nepozicované dítě
   `Stacku` se smrskne na šířku obsahu a zarovná se do levého horního rohu —
   nadpis „FS Demižón" pak přetékal přes hranu. Opraveno `width: double.infinity`.
   Statickým scanem ověřeno, že jinde se stejný vzor nevyskytuje.

2. **Přihlašování shodilo vlastní obrazovku.** `AuthController.login()` přepínalo
   stav na `AsyncLoading`, router to přes `isRestoring => isLoading` vyhodnotil
   jako obnovu session a odskočil na splash. Přihlašovací obrazovka se zahodila
   uprostřed requestu, chybová hláška se nikdy nezobrazila a následný `setState()`
   spadl na disposed widgetu.
   Opraveno na dvou úrovních: `login()` stav na `AsyncLoading` nepřepíná (spinner
   si drží obrazovka sama) a `isRestoring` je nově `isLoading && !hasValue`.
   Zamčeno regresními testy v `test/auth_state_test.dart`.

### Nativní projekty

`flutter create --platforms=android,ios --org com.demizon` → 73 souborů.
- `applicationId = com.demizon.administrace` — **shodné s MAUI**, aby šla appka
  nasadit jako update, ne jako druhá aplikace vedle stávající.
- `android:label="Demižón"` (výchozí `demizon` z generátoru nahrazeno).

## Až budeš mít Firebase a telefon

Odloženo 2026-09-09. Kód notifikací, DateTime a duálního režimu je hotový;
bez konzole a zařízení dál nejde. Až bude čas, jdi shora dolů.

1. [ ] **`flutterfire configure`** (stejný Firebase projekt, jehož service
       account poleze do kontejneru jako `FIREBASE_CREDENTIAL_JSON` — viz
       [`nasazeni.md`](../../nasazeni.md)):
       ```
       cd demizon_flutter
       dart pub global activate flutterfire_cli
       flutterfire configure --platforms=android,ios --out=lib/firebase_options.dart
       ```
       `google-services.json` patří do `android/app/` (`applicationId` je
       `com.demizon.administrace`, shodné s MAUI). `lib/firebase_options.dart`
       je gitignorovaný, stejně jako nativní config.
2. [ ] **Ikony a splash** ze `Demizon.Maui/Resources/AppIcon/` a
       `demizon_flutter/assets/images/demizon_logo.jpg`:
       `flutter_launcher_icons` + `flutter_native_splash`, pak
       `dart run flutter_launcher_icons` a `dart run flutter_native_splash:create`.
3. [ ] **Fyzický telefon** proti živému API:
       ```
       flutter run --dart-define=DEMIZON_API_BASE_URL=https://<domena>
       ```
       Proklik: přihlášení, docházka (včetně křížové tabulky a swipe měsíce),
       dual režim akce/zkouška, ťuknutí na notifikaci (cold / background /
       foreground), auth refresh po 5 min / 401.
4. [ ] Až tohle pojede: vyhodit `Demizon.Maui` z `Demizon.slnx`. Ne dřív —
       MAUI je pořád zdroj pravdy o chování.

Offline cache se záměrně nedělá (MAUI ji neměla).

## TODO

### Nutné před prvním spuštěním

- [x] ~~Nainstalovat Flutter SDK, `flutter pub get`, `build_runner`, `flutter analyze`~~
- [x] ~~`flutter create --platforms=android,ios .`~~ — hotovo, 73 souborů
- [x] ~~Zkopírovat `assets/images/demizon_logo.jpg`~~ — hotovo, včetně `.svg` varianty
- [x] ~~`applicationId` na `com.demizon.administrace`~~ — hotovo
- [x] ~~Spustit na zařízení~~ — běží na emulátoru Pixel 9 / API 36
- [ ] Firebase, ikony, fyzický telefon — **odloženo**, postup nahoře
      v *Až budeš mít Firebase a telefon*.

### Zbývá dopsat

- [x] **Notifikační stack** (2026-09-09). Přepis `NotificationNavigationService`,
      `NotificationSyncService` a `MainActivity` (kanál, foreground zobrazení, tap).
      Soubory: `lib/core/notifications/`. Tři cesty:
      cold start (`getInitialMessage` + pending replay po loginu),
      background (`onMessageOpenedApp`),
      foreground (`onMessage` → lokální notifikace na kanálu `demizon_channel`).
      Reset po odhlášení i po vypršení session. Bez `google-services.json` se FCM
      tiše přeskočí. Zamčeno v `test/notification_navigation_test.dart`.
      Android: `INTERNET` v main manifestu (release by jinak neměl síť),
      `POST_NOTIFICATIONS`, `ic_notification`, default channel/icon/color.
      Backend: automatické připomínky zkoušky teď posílají `rehearsalDate`
      (dřív `data: null`, tap neměl kam vést).
- [ ] Offline cache — MAUI ji neměla vůbec (každá stránka volá API v `OnAppearing`).
      Není to regrese, ale je to zjevné vylepšení k zvážení.

### Ověřit proti běžící aplikaci

- [x] Formát `DateTime` na drátě u zkoušek (`?date=`) — interceptor ořezává
      ISO instant z retrofitu (`toIso8601String`) na `yyyy-MM-dd`. Jinak by
      UTC pátek v CEST skočil na sobotu. Zamčeno v `test/contract_test.dart`.
      Na telefonu ověřit, že GET/PUT rehearsal sedí na ten pátek.
- [ ] Auth flow: proaktivní refresh 5 min před expirací i fallback na 401
      (kód je hotový, zbývá živé API)
- [ ] Křížová tabulka docházky — zamrzlý sloupec jmen + horizontální scroll,
      a že swipe mezi měsíci nekoliduje s vnitřním scrollem
- [x] Duální režim akce/zkouška — `EventDetailArgs.fromRoute` bere
      `/events/0?rehearsalDate=` jako zkoušku, `MemberAttendanceTarget` taky.
      Zamčeno testy. Na telefonu ověřit otevření z notifikace i z tabulky.
