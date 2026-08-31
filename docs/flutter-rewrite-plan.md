# Přepis mobilní aplikace z .NET MAUI do Flutteru

> **Živý dokument.** Průběžně aktualizovat.
> Založeno: 2026-09-01. Větev: `feat/flutter-app`. Adresář: `demizon_flutter/`.

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

Závazné konvence jsou v [`../demizon_flutter/ARCHITECTURE.md`](../demizon_flutter/ARCHITECTURE.md).

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

## ⚠️ Nic z toho není zkompilované

**Flutter SDK není na vývojovém stroji nainstalovaný** (`flutter --version` →
command not found). Veškerý Dart kód je tedy napsaný, ale neověřený. Nedořešená
místa jsou v kódu značená `// TODO(verify):`.

První kroky po instalaci SDK:

```bash
cd demizon_flutter
flutter pub get
dart run build_runner build --delete-conflicting-outputs
flutter analyze
```

Očekávat je potřeba dávku chyb z integrace — modely, API klient a obrazovky psalo
paralelně pět agentů podle společného kontraktu, takže se mohou lišit v detailech
podpisů. `flutter analyze` je najde všechny naráz.

## TODO

### Nutné před prvním spuštěním

- [ ] Nainstalovat Flutter SDK, `flutter pub get`, `build_runner`, `flutter analyze`
- [ ] `flutter create --platforms=android,ios .` v `demizon_flutter/` — dogenerovat
      nativní projekty (android/, ios/), které tu zatím nejsou
- [ ] `flutterfire configure` → `lib/firebase_options.dart`; přenést
      `google-services.json` z `Demizon.Maui/Platforms/Android/`
- [x] ~~Zkopírovat `assets/images/demizon_logo.jpg`~~ — hotovo, včetně `.svg` varianty
- [ ] Ikony a splash (`flutter_launcher_icons`, `flutter_native_splash`)
      ze `Demizon.Maui/Resources/AppIcon/`
- [ ] `applicationId` nastavit na `com.demizon.administrace` (shodné s MAUI,
      aby šlo nasadit jako update)

### Zbývá dopsat

- [ ] Notifikační stack: FCM registrace tokenu, lokální notifikace pro foreground
      zprávy, deep-linky z notifikace. Zdroje: `NotificationNavigationService.cs` (114 ř.),
      `NotificationSyncService.cs` (47 ř.), `MainActivity.cs:79-140`.
      V MAUI to byla nejkřehčí část — tři různé kódové cesty (cold start / background /
      foreground) s vlastním `lock` gate a „pending replay" mechanikou. Ve Flutteru to
      pokrývá `firebase_messaging` (`getInitialMessage()`, `onMessageOpenedApp`,
      `onMessage`) + `flutter_local_notifications`.
- [ ] Offline cache — MAUI ji neměla vůbec (každá stránka volá API v `OnAppearing`).
      Není to regrese, ale je to zjevné vylepšení k zvážení.

### Ověřit proti běžící aplikaci

- [ ] Formát `DateTime` na drátě, hlavně u endpointů zkoušek (`?date=`)
- [ ] Auth flow: proaktivní refresh 5 min před expirací i fallback na 401
- [ ] Křížová tabulka docházky — zamrzlý sloupec jmen + horizontální scroll,
      a že swipe mezi měsíci nekoliduje s vnitřním scrollem
- [ ] Duální režim akce/zkouška na detailu a v editaci docházky člena
