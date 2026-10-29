# AGENTS.md

> **Where the plan lives:** [`docs/STATUS.md`](docs/STATUS.md) is the single
> forward-looking plan — what is done, what is left, in what order. Start there.
> [`docs/README.md`](docs/README.md) indexes everything else.
>
> Documentation is organised per body of work: `docs/features/<name>/README.md`
> records a feature's brief, status and what was done, with `pruzkum.md` /
> `review.md` alongside it only when there is enough to warrant a separate file.
> Cross-cutting documents stay at the root of `docs/` (`testing-plan.md`,
> `nasazeni.md`, `notifications.md`) because they apply across features.
> A feature whose state no longer holds moves to `docs/features/archiv/`.
>
> **Records of finished work are not TODO lists.** Anything under a closed
> feature or `archiv/` describes what happened and why, including what was
> deliberately *not* done — read that before "fixing" something that is
> intentional.

## Scope and current source of truth
- Work primarily in `Demizon.Mvc` (Blazor Server + API in one host); this is the only web host. Flutter talks to it. There is no `Demizon.Api` project any more (removed in `71d9916`).
- Shared layers: `Demizon.Contracts` (DTOs), `Demizon.Core` (business services), `Demizon.Dal` (EF Core + SQLite), `Demizon.Common` (settings/exceptions/helpers).
- Mobile client: `demizon_flutter/` is the only maintained one. `Demizon.Maui` is **no longer maintained** (decided 2026-10-26): it is out of `Demizon.slnx`, does not compile against the current contracts, and API changes do not keep it working. The folder stays only as a historical reference. See `docs/features/flutter-prepis/`.
- Tests: `Demizon.Tests.Unit` (fast logic + HTTP via `WebApplicationFactory`), `Demizon.Tests.Integration` (real SQLite), `Demizon.Tests.E2E` (Playwright against the running app).

## Architecture map (how data flows)
- UI/API entrypoint: `Demizon.Mvc/Program.cs` wires Razor, Blazor, JWT+cookie auth, hosted services, DB, migrations, WAL mode.
- Controllers in `Demizon.Mvc/Controllers/Api/*` call Core services, then map entities to DTOs via `Demizon.Mvc/Mapping/ContractMappingExtensions.cs`.
- Core DI is centralized in `Demizon.Core/Extensions/CoreServicesRegistrationExtension.cs`; add service registrations there.
- Persistence is centralized in `Demizon.Dal/DemizonContext.cs` + `Demizon.Dal/Extensions/DatabaseServiceConfigurationExtension.cs`.
- Flutter consumes API contracts via dio + retrofit (`demizon_flutter/lib/api/api_client.dart`). MAUI's Refit `IApiClient.cs` is historical only.

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
- **Permissions are roles in `MemberRoles`, several per member** (`UserRole`: `Admin`, `ContentManager`).
  A member with no role is an ordinary member, so "any logged-in user" is plain `[Authorize]`, never a
  role list. Use the constants in `Demizon.Mvc/Services/Authentication/RoleNames.cs`, not string literals,
  for new checks. `IsDancer` / `IsMusician` / `IsExternal` describe what someone does in the ensemble and
  are **not** roles. Details: `docs/features/role-clenu/`.
- **Costume store (`docs/features/krojarka/`): costume entities reference members by foreign key only, never by
  navigation.** A required navigation to the soft-deleted `Member` would make `Include` drop the rows of a former
  member who still holds a costume. Resolve names with `ICostumeCatalogService.GetMemberNamesAsync`. Transfer rules
  (who may create, confirm, cancel) live in `CostumeTransferService`; side effects (push) go through `CostumeWorkflow`.
  SQLite returns `DateTime` without `Kind` — mark UTC before serialising (`CostumeContractMapping`).
- **A MudBlazor dialog form must validate before closing.** `Required="true"` on a field only draws
  the marker; enforcing it needs a `MudForm` plus a validity check in the OK handler.
- Keep contract boundary strict: API returns `Demizon.Contracts.*` DTOs; do not expose EF entities directly.
- Attendance status contract is lowercase strings (`"yes"|"maybe"|"no"`), parsed in controllers (see `AttendancesController.ParseStatus`).
- Rehearsals are virtual: attendance rows with `EventId == null` and `Date` = midnight of the rehearsal day. **Which days are rehearsals is decided only by `IRehearsalService` / `RehearsalSchedule`** (Friday unless a non-cancelled event covers it, minus `RehearsalCancellation` ranges) — never recompute "is it Friday" elsewhere; it used to be copied five times and the copies drifted. Side effects of calendar changes (Google Calendar, push) go through `Demizon.Mvc/Services/Rehearsal/RehearsalWorkflow.cs`.
- `Event.DateFrom`/`DateTo` are **local Prague wall-clock time**, not UTC (clients send them without offset; `UtcDateConvertExtension` is not applied). Do not convert them from UTC.
- Member soft delete is implemented by EF global query filter (`Member.DeletedAt == null`) in `DemizonContext`; avoid bypassing with raw SQL unless intentional.
- Audit logging is automatic through `AuditSaveChangesInterceptor`; `ICurrentUserAccessor` is expected to be available in web hosts.
- SQLite concurrency is intentional: app startup calls `EnableWalMode()` so the host and `sqlite3` backup can share the same file.

## Auth and client integration details
- MVC host uses cookie auth as default (Blazor) and JWT bearer for `/api/*` (`MvcAuthenticationServicesRegistrationExtension.cs`).
- JWT member id is stored in `ClaimTypes.PrimarySid`; use `User.GetMemberId()` extension. Every role is its own `ClaimTypes.Role` claim; the token response carries them as `roles: string[]`.
- Sessions are re-checked against the DB (`MemberSessionValidator`): a cookie whose roles no longer match is signed out, a JWT gets 401 (the client refreshes into current roles). A test that hand-crafts a JWT must therefore use a member that exists in the DB with exactly those roles.
- Flutter token lifecycle: `TokenStorage` + `AuthInterceptor` (proactive refresh 5 min before expiry, fallback on 401).
- Flutter uses hierarchical `go_router` paths in `demizon_flutter/lib/core/routes.dart`.
- Flutter colours: text and tinted backgrounds come from `context.palette` (`DemizonPalette` in `core/theme.dart`),
  never `DemizonColors.textPrimary` / `textSecondary` directly — those are light-theme values and vanish in dark mode.
- The Flutter theme gives `FilledButton` a full-width minimum size. Inside a `Row` override it
  (`FilledButton.styleFrom(minimumSize: const Size(0, 48))`), otherwise layout throws and the whole row disappears.

## Notifications and external integrations
- FCM mobile push: `Demizon.Mvc/Services/FcmService.cs`, device tokens in `DeviceTokens` table, endpoints in `Controllers/Api/NotificationsController.cs`.
- Web Push (browser): `Services/Notification/WebPushSender.cs` uses VAPID settings and `PushSubscriptions`; it is driven by `UnifiedNotificationService` (the only registered notification hosted service).
- Google Calendar sync is triggered by attendance updates (`AttendancesController`) and OAuth endpoints (`/google/connect`, `/google/callback`) in `Program.cs`.

## Configuration, secrets, and local setup
- MVC config load order is `appsettings.Local.json` -> `appsettings.json` -> `appsettings.{Environment}.json` -> **environment variables** (see `Program.cs`). The trailing `AddEnvironmentVariables()` matters: without it the re-added json files sit after the defaults and silently win over `-e` from Docker.
- `Jwt__SecretKey` has no value in any `appsettings.json` and is required — the host will not start without it.
- The first admin is created through `POST /api/database/seed`, which is gated behind `Bootstrap__SeedToken` and refuses to run once any member exists. Procedure in `docs/nasazeni.md`.
- Use `Demizon.Mvc/appsettings.Local.json.example` as template; real local file is gitignored (`.gitignore`).

## Working commands (from repo root)
- Restore/build main solution: `dotnet restore Demizon.slnx` then `dotnet build Demizon.slnx`.
- The solution is the **XML `.slnx` format** (migrated 2026-09-02); there is no `Demizon.sln` any more.
  Keeping both would make every solution-discovering tool ambiguous (`MSB1011`).
- `dotnet build Demizon.slnx` builds everything (no MAUI since 2026-10-26). For tests use the filters:
  - `dotnet test Demizon.Backend.slnf` — unit + integration. The fast gate; no external dependency.
  - `dotnet test Demizon.E2E.slnf` — Playwright in a real browser. Needs Chromium once:
    `pwsh Demizon.Tests.E2E/bin/Release/net10.0/playwright.ps1 install chromium`.
  E2E is a separate filter on purpose, so the fast gate does not pull a 150 MB browser. CI runs them
  as two jobs (`.github/workflows/test.yml`).
- Run primary host (UI + API): `dotnet run --project Demizon.Mvc/Demizon.Mvc.csproj` (ports from `Demizon.Mvc/Properties/launchSettings.json`).
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
