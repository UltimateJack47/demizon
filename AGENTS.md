# AGENTS.md

## Scope and current source of truth
- Work primarily in `Demizon.Mvc` (Blazor Server + API in one host); this is the only web host in `Demizon.sln`.
- `Demizon.Api` contains a parallel standalone API host (same domain concepts/controllers), useful for API-only runs but not part of the solution file.
- Shared layers: `Demizon.Contracts` (DTOs), `Demizon.Core` (business services), `Demizon.Dal` (EF Core + SQLite), `Demizon.Common` (settings/exceptions/helpers), `Demizon.Maui` (mobile client).

## Architecture map (how data flows)
- UI/API entrypoint: `Demizon.Mvc/Program.cs` wires Razor, Blazor, JWT+cookie auth, hosted services, DB, migrations, WAL mode.
- Controllers in `Demizon.Mvc/Controllers/Api/*` call Core services, then map entities to DTOs via `Demizon.Mvc/Mapping/ContractMappingExtensions.cs`.
- Core DI is centralized in `Demizon.Core/Extensions/CoreServicesRegistrationExtension.cs`; add service registrations there.
- Persistence is centralized in `Demizon.Dal/DemizonContext.cs` + `Demizon.Dal/Extensions/DatabaseServiceConfigurationExtension.cs`.
- MAUI consumes API contracts via Refit interface `Demizon.Maui/Services/IApiClient.cs`.

## Project-specific patterns you must follow
- Keep contract boundary strict: API returns `Demizon.Contracts.*` DTOs; do not expose EF entities directly.
- Attendance status contract is lowercase strings (`"yes"|"maybe"|"no"`), parsed in controllers (see `AttendancesController.ParseStatus`).
- Rehearsals are modeled as attendance rows with `EventId == null` and Friday date semantics (see `EventsController.GetByMonth`, `AttendancesController` rehearsal endpoints).
- Member soft delete is implemented by EF global query filter (`Member.DeletedAt == null`) in `DemizonContext`; avoid bypassing with raw SQL unless intentional.
- Audit logging is automatic through `AuditSaveChangesInterceptor`; `ICurrentUserAccessor` is expected to be available in web hosts.
- SQLite concurrency is intentional: app startup calls `EnableWalMode()` to support multi-process access (Mvc + Api).

## Auth and client integration details
- MVC host uses cookie auth as default (Blazor) and JWT bearer for `/api/*` (`MvcAuthenticationServicesRegistrationExtension.cs`).
- JWT member id is stored in `ClaimTypes.PrimarySid`; use `User.GetMemberId()` extension.
- MAUI token lifecycle: `TokenStorage` + `AuthHandler` + `TokenRefreshHelper`; refresh happens proactively and on 401.
- MAUI navigation rules are strict: constants in `Demizon.Maui/AppRoutes.cs`, detail routes must be flat names (no `/`).

## Notifications and external integrations
- FCM mobile push: `Demizon.Mvc/Services/FcmService.cs`, device tokens in `DeviceTokens` table, endpoints in `Controllers/Api/NotificationsController.cs`.
- Web Push (browser): `NotificationHostedService` uses VAPID settings and `PushSubscriptions`.
- Google Calendar sync is triggered by attendance updates (`AttendancesController`) and OAuth endpoints (`/google/connect`, `/google/callback`) in `Program.cs`.

## Configuration, secrets, and local setup
- MVC config load order is `appsettings.Local.json` -> `appsettings.json` -> `appsettings.{Environment}.json` (see `Program.cs`).
- Use `Demizon.Mvc/appsettings.Local.json.example` as template; real local file is gitignored (`.gitignore`).
- MAUI Android expects local secret files: `Platforms/Android/FirebaseConfig.cs` and `google-services.json` (both gitignored; template provided).

## Working commands (from repo root)
- Restore/build main solution: `dotnet restore Demizon.sln` then `dotnet build Demizon.sln`.
- Run primary host (UI + API): `dotnet run --project Demizon.Mvc/Demizon.Mvc.csproj` (ports from `Demizon.Mvc/Properties/launchSettings.json`).
- Run standalone API host if needed: `dotnet run --project Demizon.Api/Demizon.Api.csproj`.
- EF migrations: use MVC as startup host, e.g. `dotnet ef migrations add <Name> --project Demizon.Dal --startup-project Demizon.Mvc`.
