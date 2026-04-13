# Demizon — Implementační plán: Api + Maui

**Stav:** 🔲 Čeká na implementaci  
**Poslední aktualizace:** 2026-04-13

---

## Context

Přidání mobilní aplikace pro členy folklorního souboru. Existující Blazor Server (Demizon.Mvc) zůstává beze větších změn — nové projekty `Demizon.Api` a `Demizon.Maui` jsou přidány vedle něj. Sdílené DTO typy žijí v novém `Demizon.Contracts`.

**Primární use-case:** Správa vlastní docházky na akce  
**Vedlejší use-case:** Prohlížení tanců a jejich detailů  
**Doplňkové funkce:** Push notifikace pro nevyplněnou docházku + Google Calendar sync

---

## Výsledná struktura solution

```
Demizon.Common      [existující, beze změn]
Demizon.Dal         [existující + DeviceToken entita + WAL mode]
Demizon.Core        [existující + TokenService/RefreshTokenService přesun + FcmService]
Demizon.Contracts   [NOVÝ — sdílené DTO záznamy]
Demizon.Api         [NOVÝ — ASP.NET Core Web API]
Demizon.Mvc         [existující, update using po přesunu auth services]
Demizon.Maui        [NOVÝ — .NET MAUI Android + iOS]
```

```
Závislosti:
Demizon.Maui → Demizon.Contracts
Demizon.Api  → Core + Dal + Common + Contracts
Demizon.Core → Dal + Common  (+TokenService, RefreshTokenService, FcmService)
Demizon.Mvc  → Core + Dal + Common  [beze změny]
```

---

## Fáze 0 — Uložení plánu do docs

| # | Úkol | Stav |
|---|---|---|
| 0.1 | Zkopírovat plán do `docs/maui-api-plan.md` | ✅ |

---

## Fáze 1 — Přesun auth services do Core

> **Proč:** `TokenService` a `RefreshTokenService` závisí jen na Dal+Common, patří do Core. `Demizon.Api` je potřebuje bez odkazu na Mvc.

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 1.1 | Přesunout `TokenService.cs` do Core | `Demizon.Mvc/Services/Authentication/TokenService.cs` → `Demizon.Core/Services/Authentication/` | ✅ |
| 1.2 | Přesunout `RefreshTokenService.cs` do Core | `Demizon.Mvc/Services/Authentication/RefreshTokenService.cs` → `Demizon.Core/Services/Authentication/` | ✅ |
| 1.3 | Aktualizovat namespace v obou souborech | namespace `Demizon.Core.Services.Authentication` | ✅ |
| 1.4 | Přidat registrace do CoreServicesRegistrationExtension | `Demizon.Core/Extensions/CoreServicesRegistrationExtension.cs` — `AddScoped<TokenService>()` + `AddScoped<RefreshTokenService>()` | ✅ |
| 1.5 | Odebrat registrace z MvcAuthenticationServicesRegistrationExtension | `Demizon.Mvc/Services/Authentication/MvcAuthenticationServicesRegistrationExtension.cs` | ✅ |
| 1.6 | Aktualizovat using v AuthenticationService.cs | `Demizon.Mvc/Services/Authentication/AuthenticationService.cs` | ✅ |
| 1.7 | Aktualizovat using v Program.cs (ř. 229) | `Demizon.Mvc/Program.cs` | ✅ |
| 1.8 | Build + ověřit funkčnost Mvc | `dotnet build` + spustit Mvc | ✅ |

---

## Fáze 2 — Dal změny: DeviceToken entita + WAL mode

> **Proč:** FCM push notifikace vyžadují uložení device tokenů per-member. WAL mode umožní souběžný přístup Mvc + Api ke stejnému SQLite souboru.

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 2.1 | Vytvořit `DeviceToken` entitu | `Demizon.Dal/Entities/DeviceToken.cs` — Id, MemberId (FK), Token (string), Platform ("android"/"ios"), CreatedAt, LastSeenAt | 🔲 |
| 2.2 | Přidat `DbSet<DeviceToken>` do DbContextu | `Demizon.Dal/DemizonContext.cs` | 🔲 |
| 2.3 | Přidat `TokenPrefix` sloupec do `RefreshToken` entity | `Demizon.Dal/Entities/RefreshToken.cs` — `string TokenPrefix` (prvních 8 znaků raw tokenu, plaintext); aktualizovat `RefreshTokenService.CreateAsync` pro uložení prefixu a `ValidateAsync` pro filtrování `WHERE TokenPrefix = @prefix` před bcrypt ověřením — odstraní O(n) scan všech tokenů | 🔲 |
| 2.4 | Přidat WAL mode pragma | `Demizon.Dal/Extensions/DatabaseServiceConfigurationExtension.cs` — `PRAGMA journal_mode=WAL` při startu | 🔲 |
| 2.5 | Vytvořit EF migraci | `dotnet ef migrations add AddDeviceTokenWalModeAndRefreshTokenPrefix` | 🔲 |
| 2.6 | Ověřit migraci | Spustit Mvc → DB se aktualizuje bez chyb | 🔲 |

---

## Fáze 3 — Vytvoření Demizon.Contracts

> **Proč:** ViewModels v Mvc mají závislost na `MudBlazor.DateRange` a `CryptoHelper` — nelze sdílet s MAUI. Contracts je čistá class library bez závislostí.

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 3.1 | Vytvořit projekt + přidat do solution | `Demizon.Contracts/Demizon.Contracts.csproj` (net10.0, žádné NuGet, žádné project refs) | 🔲 |
| 3.2 | Auth DTOs | `Auth/TokenRequest.cs`, `Auth/TokenResponse.cs`, `Auth/RefreshRequest.cs` | 🔲 |
| 3.3 | Events DTOs | `Events/EventDto.cs` — Id, Name, DateFrom, DateTo, Place, IsCancelled, RecurrenceType | 🔲 |
| 3.4 | Attendance DTOs | `Attendances/AttendanceDto.cs`, `Attendances/UpsertAttendanceRequest.cs` — bool? Attends, string? Comment, string? Role | 🔲 |
| 3.5 | Dances DTOs | `Dances/DanceDto.cs`, `Dances/VideoLinkDto.cs` | 🔲 |
| 3.6 | Members DTOs | `Members/MemberProfileDto.cs` — bez PasswordHash | 🔲 |
| 3.7 | Notifications DTOs | `Notifications/RegisterDeviceRequest.cs` — record(string Token, string Platform) | 🔲 |
| 3.8 | Build Contracts | `dotnet build Demizon.Contracts` | 🔲 |

---

## Fáze 4 — Vytvoření Demizon.Api

> **Proč:** Blazor Server není vhodný backend pro nativní mobilní klient. Čistý Web API projekt s JWT-only auth.

### 4A — Základní infrastruktura

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 4.1 | Vytvořit projekt + přidat do solution | `Demizon.Api/Demizon.Api.csproj` — `Microsoft.NET.Sdk.Web`, `net10.0`, refs: Core+Dal+Common+Contracts | 🔲 |
| 4.2 | Přidat NuGet balíčky | `JwtBearer`, `CryptoHelper`, `Scalar.AspNetCore`, `FirebaseAdmin`, `HealthChecks.EntityFrameworkCore` | 🔲 |
| 4.3 | `appsettings.json` | Connection string na stejný `.sqlite` soubor, sekce `Jwt` (stejné hodnoty jako Mvc), `Firebase` sekce | 🔲 |
| 4.4 | `ApiAuthServicesExtension.cs` | JWT-only auth (bez cookies), stejné `JwtSettings` z Common | 🔲 |
| 4.4b | Ověřit využití `TokenService.ValidateToken` | JwtBearer middleware pokrývá standardní validaci automaticky — pokud žádný controller/handler `ValidateToken` nevolá manuálně, metodu odebrat z `TokenService` | 🔲 |
| 4.5 | `CurrentUserAccessor.cs` | Implementace `ICurrentUserAccessor` čtením z JWT claims přes `IHttpContextAccessor` | 🔲 |
| 4.6 | `ContractMappingExtensions.cs` | Extension metody: entita → DTO (Events, Attendances, Dances) | 🔲 |
| 4.7 | `Program.cs` | JWT auth, CORS, rate limiter (auth endpointy), health checks, Scalar OpenAPI, WAL mode | 🔲 |

### 4B — Controllery

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 4.8 | `AuthController.cs` | `POST /api/auth/token`, `POST /api/auth/refresh` (přepoužít TokenService+RefreshTokenService z Core) | 🔲 |
| 4.9 | `EventsController.cs` | `GET /api/events/upcoming`, `GET /api/events/{id}` (s mou docházkou v odpovědi) | 🔲 |
| 4.10 | `AttendancesController.cs` | `GET /api/attendances/me`, `PUT /api/attendances/{eventId}` (+ Google Calendar sync přes `IGoogleCalendarService`) | 🔲 |
| 4.11 | `DancesController.cs` | `GET /api/dances`, `GET /api/dances/{id}` (s videi) | 🔲 |
| 4.12 | `NotificationsController.cs` | `POST /api/notifications/device`, `DELETE /api/notifications/device` | 🔲 |

### 4C — Push notifikace

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 4.13 | `FcmService.cs` | Wrapper nad Firebase Admin SDK pro odesílání FCM zpráv na device token | 🔲 |
| 4.14 | Background service pro nevyplněnou docházku | Periodická kontrola: člen má akci bez záznamu docházky → FCM push | 🔲 |

### 4D — Ověření Api

| # | Úkol | Stav |
|---|---|---|
| 4.15 | Build celého solution | 🔲 |
| 4.16 | Spustit Mvc + Api současně → žádný SQLite lock conflict | 🔲 |
| 4.17 | `POST /api/auth/token` → JWT token | 🔲 |
| 4.18 | `GET /api/events/upcoming` s Bearer tokenem → seznam akcí | 🔲 |
| 4.19 | `PUT /api/attendances/{id}` → uložení + Google Calendar sync (pokud propojeno) | 🔲 |
| 4.20 | Scalar OpenAPI UI dostupný v dev mode | 🔲 |

---

## Fáze 5 — Vytvoření Demizon.Maui

> **Proč:** Nativní mobilní app pro Android + iOS. Komunikuje výhradně s Demizon.Api přes Refit HTTP klienty.

### 5A — Základní infrastruktura

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 5.1 | Vytvořit MAUI projekt + přidat do solution | `Demizon.Maui/Demizon.Maui.csproj` — `<UseMaui>true</UseMaui>`, `net10.0-android;net10.0-ios`, ref: Contracts | 🔲 |
| 5.2 | Přidat NuGet balíčky | `CommunityToolkit.Maui`, `CommunityToolkit.Mvvm`, `Refit`, `Refit.HttpClientFactory`, `Plugin.Firebase.CloudMessaging` | 🔲 |
| 5.3 | Firebase setup — Android | `Platforms/Android/google-services.json` + inicializace FCM v `MainApplication.cs` | 🔲 |
| 5.4 | Firebase setup — iOS | `Platforms/iOS/GoogleService-Info.plist` + inicializace v `AppDelegate.cs` | 🔲 |
| 5.5 | `TokenStorage.cs` | Wrapper nad `SecureStorage.Default` pro uložení JWT + refresh tokenu | 🔲 |
| 5.6 | `AuthHandler.cs` | `DelegatingHandler`: přidá Bearer header, na 401 refresh → retry jednou | 🔲 |
| 5.7 | `IApiClient.cs` | Refit interface pro všechny endpointy s Contracts typy | 🔲 |
| 5.8 | `MauiProgram.cs` | DI setup: Refit client, AuthHandler, ViewModels, base URL (`#if ANDROID` → `10.0.2.2`) | 🔲 |

### 5B — Shell a navigace

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 5.9 | `AppShell.xaml` | Tab bar: **Akce** / **Tance**; LoginPage jako pre-auth route (bez tabů) | 🔲 |

### 5C — Login

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 5.10 | `LoginViewModel.cs` | Login/heslo → `POST /api/auth/token` → uložit do TokenStorage → navigace na Shell | 🔲 |
| 5.11 | `LoginPage.xaml` | Formulář přihlášení | 🔲 |

### 5D — Akce a docházka (primární use-case)

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 5.12 | `EventsViewModel.cs` | `GET /api/events/upcoming` → seznam s indikátorem stavu docházky (vyplněno/nevyplněno) | 🔲 |
| 5.13 | `EventsPage.xaml` | Seznam nadcházejících akcí s barevným indikátorem docházky | 🔲 |
| 5.14 | `EventDetailViewModel.cs` | Detail akce + inline formulář docházky: přijdu/nepřijdu + role + komentář → `PUT /api/attendances/{id}` | 🔲 |
| 5.15 | `EventDetailPage.xaml` | Detail + formulář docházky | 🔲 |

### 5E — Tance (vedlejší use-case)

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 5.16 | `DancesViewModel.cs` | `GET /api/dances` | 🔲 |
| 5.17 | `DancesPage.xaml` | Seznam tanců | 🔲 |
| 5.18 | `DanceDetailViewModel.cs` | `GET /api/dances/{id}` s videi | 🔲 |
| 5.19 | `DanceDetailPage.xaml` | Detail tance s přehráváním videí | 🔲 |

### 5F — Push notifikace

| # | Úkol | Soubor | Stav |
|---|---|---|---|
| 5.20 | Registrace FCM tokenu po přihlášení | Po `LoginViewModel` success: získat FCM token → `POST /api/notifications/device` | 🔲 |
| 5.21 | Zpracování příchozí push notifikace | Handler pro příchozí FCM zprávu → navigace na EventDetailPage | 🔲 |

### 5G — Ověření MAUI

| # | Úkol | Stav |
|---|---|---|
| 5.22 | Přihlášení na Android emulátoru | 🔲 |
| 5.23 | Seznam akcí se zobrazí, docházka jde uložit | 🔲 |
| 5.24 | Google Calendar sync — uložená docházka se propíše do kalendáře (ověřit přes web) | 🔲 |
| 5.25 | FCM push doručen na emulátor pro nevyplněnou docházku | 🔲 |
| 5.26 | Seznam tanců s detailem funguje | 🔲 |

---

## Klíčová rizika

| Riziko | Mitigace |
|---|---|
| SQLite souběžný přístup Mvc + Api | WAL mode + busy timeout (fáze 2.3) |
| FCM vyžaduje Firebase projekt + konfigurační soubory | Vytvořit Firebase projekt před fází 4.13 a 5.3 |
| APNs na iOS → Apple Developer Program ($99/rok) | iOS push testovat až po registraci; Android emulátor postačí pro MVP |
| Android emulátor → `localhost` nedosažitelný | `#if ANDROID` switch v MauiProgram.cs → `10.0.2.2` |
| Google Calendar OAuth v MAUI přes systémový prohlížeč | Funguje jen pokud je Mvc veřejně dostupné; pro dev použít ngrok |
