# AGENTS.md

> **Where the plan lives:** [`docs/README.md`](docs/README.md) is the index of
> all documentation; [`docs/next-wave-plan.md`](docs/next-wave-plan.md) is the
> single forward-looking plan (what is done, what is left, in what order).
> Most other files under `docs/` are records of finished work — do not read
> them as TODO lists.

## Scope and current source of truth
- Work primarily in `Demizon.Mvc` (Blazor Server + API in one host); this is the only web host in `Demizon.slnx`.
- `Demizon.Api` contains a parallel standalone API host (same domain concepts/controllers), useful for API-only runs but not part of the solution file.
- Shared layers: `Demizon.Contracts` (DTOs), `Demizon.Core` (business services), `Demizon.Dal` (EF Core + SQLite), `Demizon.Common` (settings/exceptions/helpers).
- Mobile client: `demizon_flutter/` is the one being built; `Demizon.Maui` is the predecessor it replaces and is kept as the reference for behaviour still to be ported (notifications, navigation). See `docs/flutter-rewrite-plan.md`.
- Tests: `Demizon.Tests.Unit` (fast logic + HTTP via `WebApplicationFactory`), `Demizon.Tests.Integration` (real SQLite), `Demizon.Tests.E2E` (Playwright against the running app).

## Architecture map (how data flows)
- UI/API entrypoint: `Demizon.Mvc/Program.cs` wires Razor, Blazor, JWT+cookie auth, hosted services, DB, migrations, WAL mode.
- Controllers in `Demizon.Mvc/Controllers/Api/*` call Core services, then map entities to DTOs via `Demizon.Mvc/Mapping/ContractMappingExtensions.cs`.
- Core DI is centralized in `Demizon.Core/Extensions/CoreServicesRegistrationExtension.cs`; add service registrations there.
- Persistence is centralized in `Demizon.Dal/DemizonContext.cs` + `Demizon.Dal/Extensions/DatabaseServiceConfigurationExtension.cs`.
- MAUI consumes API contracts via Refit interface `Demizon.Maui/Services/IApiClient.cs`.

## Project-specific patterns you must follow
- **Core write operations return `Result` / `Result<T>`, never `bool`.** `CreateAsync` and
  `CreateOrUpdateAsync` return `Result<int>` carrying the new key; `DeleteAsync` returns `Result`.
  `Result.ErrorKind` (`None`/`Failure`/`NotFound`/`Rejected`) is what controllers map to a status
  code via `Demizon.Mvc/Extensions/ResultHttpExtensions.cs` — do not parse the error text.
- **Never ignore the returned `Result`.** The compiler will not warn you (C# has no `[[nodiscard]]`,
  and CA1806 only covers `[Pure]` methods, which these are not). Every discarded result used to be a
  place that reported success on a failed write; that is the bug class the type exists to prevent.
  Show `Result.Error` to the user — for a `Rejected` result it is the only actionable information.
- **Core services never throw from write operations.** They swallow, clean the change tracker via
  `DiscardPendingChanges()`, and return a failure. Callers rely on that and wrap none of these calls
  in `try/catch`, so keep entity lookups *inside* the `try` and return `NotFound` for a missing row.
- **A MudBlazor dialog form must validate before closing.** `Required="true"` on a field only draws
  the marker; enforcing it needs a `MudForm` plus a validity check in the OK handler.
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
- Web Push (browser): `Services/Notification/WebPushSender.cs` uses VAPID settings and `PushSubscriptions`; it is driven by `UnifiedNotificationService` (the only registered notification hosted service).
- Google Calendar sync is triggered by attendance updates (`AttendancesController`) and OAuth endpoints (`/google/connect`, `/google/callback`) in `Program.cs`.

## Configuration, secrets, and local setup
- MVC config load order is `appsettings.Local.json` -> `appsettings.json` -> `appsettings.{Environment}.json` -> **environment variables** (see `Program.cs`). The trailing `AddEnvironmentVariables()` matters: without it the re-added json files sit after the defaults and silently win over `-e` from Docker.
- `Jwt__SecretKey` has no value in any `appsettings.json` and is required — the host will not start without it.
- The first admin is created through `POST /api/database/seed`, which is gated behind `Bootstrap__SeedToken` and refuses to run once any member exists. Procedure in `docs/hosting-optimization-plan.md`.
- Use `Demizon.Mvc/appsettings.Local.json.example` as template; real local file is gitignored (`.gitignore`).
- MAUI Android expects local secret files: `Platforms/Android/FirebaseConfig.cs` and `google-services.json` (both gitignored; template provided).

## Working commands (from repo root)
- Restore/build main solution: `dotnet restore Demizon.slnx` then `dotnet build Demizon.slnx`.
- The solution is the **XML `.slnx` format** (migrated 2026-09-02); there is no `Demizon.sln` any more.
  Keeping both would make every solution-discovering tool ambiguous (`MSB1011`).
- `Demizon.slnx` includes `Demizon.Maui`, which needs the `maui-android` workload and will not build
  on a machine without it. Use the solution filters instead:
  - `dotnet test Demizon.Backend.slnf` — unit + integration. The fast gate; no external dependency.
  - `dotnet test Demizon.E2E.slnf` — Playwright in a real browser. Needs Chromium once:
    `pwsh Demizon.Tests.E2E/bin/Release/net10.0/playwright.ps1 install chromium`.
  E2E is a separate filter on purpose, so the fast gate does not pull a 150 MB browser. CI runs them
  as two jobs (`.github/workflows/test.yml`).
- Run primary host (UI + API): `dotnet run --project Demizon.Mvc/Demizon.Mvc.csproj` (ports from `Demizon.Mvc/Properties/launchSettings.json`).
- Run standalone API host if needed: `dotnet run --project Demizon.Api/Demizon.Api.csproj`.
- EF migrations: use MVC as startup host, e.g. `dotnet ef migrations add <Name> --project Demizon.Dal --startup-project Demizon.Mvc`.

## Environment traps worth knowing (each cost a debugging session)

- In `Development` **no exception handler is registered** and there is no developer exception page:
  an unhandled exception becomes HTTP 500 with an empty body and nothing in the log. Run the app for
  real and read the console when a test reports an unexplained 500.
- `dotnet run` takes its URL from `launchSettings.json` and ignores `ASPNETCORE_URLS`. Run the built
  dll directly when the port matters.
- Validation attributes on a `record` belong on the **primary constructor parameters**, not on the
  properties. .NET 10 throws while building action metadata for `[property: Required]`, which turns
  every request to that endpoint into a 500 regardless of input.
- The culture of a Blazor **circuit** comes from the request that establishes it (the WebSocket
  handshake on `/_blazor`), not from the `_Host` page request. Content rendered by the circuit
  therefore cannot be language-tested through Playwright, whose header overrides do not apply to that
  handshake. A real browser sends `Accept-Language` there, so users do get Czech.
- A running local host locks the built dlls; stop it before rebuilding.
