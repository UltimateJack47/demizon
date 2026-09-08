# Stav po diskové optimalizaci a další vlna

> **Živý dokument.** Průběžně aktualizovat při každé dokončené položce.
> Založeno: 2026-09-08. Poslední aktualizace: 2026-09-08.
>
> Účel: předat kontext další session (Claude / kdokoli) — co je hotové, na co
> nesahej, co zbývá a v jakém pořadí. Disková vlna na Scaleway Stardust je
> v kódu uzavřená. Další práce je produkt, provoz a zbylé backend díry.

Související dokumenty (podrobnosti nekopírovat sem, jen odkazovat):

| Dokument | Co v něm hledat |
|---|---|
| [`AGENTS.md`](../AGENTS.md) | architektura, kontrakty, příkazy |
| [`hosting-optimization-plan.md`](hosting-optimization-plan.md) | diagnóza disku/RAM, P1–P3, `docker run` recept |
| [`testing-plan.md`](testing-plan.md) | co testy hlídají, nalezené chyby, konvence |
| [`flutter-rewrite-plan.md`](flutter-rewrite-plan.md) | 1:1 přepis MAUI → Flutter |
| [`notifications.md`](notifications.md) | milníky, kanály FCM / Web Push |

**Větev:** `master` (= `origin/master`). **Host:** `Demizon.Mvc` (Blazor Server + API).
Testy: `dotnet test Demizon.Backend.slnf` — ne `Demizon.slnx` (MAUI chce `maui-android`).

---

## Zadání, které tuhle vlnu uzavřelo

Poslední prompt před pádem Claude session (limit):

- přepsat znění u VAPID klíčů
- naměřit RSS Blazor okruhů, Docker už byl k dispozici
- volné ruce na code review a automatizované testy důležitých částí
- commity přímo do `master` (předchozí disková práce už byla pushnutá a mergnutá)

---

## Commity na `master` z této vlny

```
9a88daa docs(disk): circuit RSS measurement, VAPID deploy wording, auth tests
2d932d0 test: HTTP auth contract, JWT claims, and CI on push
728267f fix(auth): MemberId in token response, JWT admin schemes, env overlay
```

Před nimi poslední diskový commit: `077dff4 docs(disk): record image smoke test and the required Jwt__SecretKey`.

Testy v okamžiku push: **96 unit + 155 integration = 251**, vše zelené.
CI: [`.github/workflows/test.yml`](../.github/workflows/test.yml) — `dotnet test Demizon.Backend.slnf` na push/PR.

---

## Co se změnilo v kódu (`728267f`)

### 1. `TokenResponse.MemberId` se nikdy neplnilo

Kontrakt pole má, Flutter (`token_storage.dart`) i MAUI ho ukládají jen když
`MemberId != 0`. `AuthController.Token` i `Refresh` ho do odpovědi nepředávaly,
klient po přihlášení neměl ID člena.

**Oprava:** do `TokenResponse` se předává `member.Id`.
Hlídá `AuthApiTests.Token_vrati_jwt_s_member_id_a_roli`.

### 2. Admin mutace na Events/Videos chtěly cookie, ne JWT

Class-level `[Authorize(AuthenticationSchemes = JwtBearer)]` + method-level
`[Authorize(Roles = "Admin")]` bez schématu = druhé bere výchozí cookie.
JWT z Flutteru by na DELETE/PUT dostal 401. Dva notify endpointy JwtBearer
už měly — ostatní ne.

**Oprava:** všechny admin akce na `EventsController` a `VideosController` mají
explicitně JwtBearer + Admin.
Hlídá `AuthApiTests.Admin_endpoint_*`.

### 3. Seed hashoval jiné heslo, než vracel

`DatabaseController.SeedDatabase` hashoval `"testpass"`, v JSON posílal
`password = "admin123"`.

**Oprava:** hashuje se `admin123`.
**Neopraveno:** endpoint je pořád `[AllowAnonymous]` a heslo vrací v těle
odpovědi — viz vlna A.

### 4. Env proměnné přebíjely json soubory

`Program.cs` po `CreateBuilder` znovu nahrával tři json. Env z Dockeru
(`-e AllowedHosts`, `ConnectionStrings__Default`) by se ignorovaly.

**Oprava:** po `AddJsonFile` následuje `.AddEnvironmentVariables()`.
Ověřeno: `http://127.0.0.1/health` proti `AllowedHosts=localhost` → 400;
`http://localhost/health` → 200.

### 5. Blazor okruhy 10 × 1 min → 30 × 3 min

Původní hodnota byla naslepo. `_Host.cshtml` dává okruh i anonymní návštěvě,
takže veřejný provoz vytlačoval adminův odpojený formulář docházky.

Rate limiter auth čte `RateLimiting:AuthPermitLimit` (default 5). Test host
nastavuje 10000, jinak by se suite trefila do 429.

---

## RSS měření (2026-09-08)

Image `demizon-mvc:rss-test`, `--memory=768m`. Metrika **VmRSS** procesu
(ne docker stats na Windows — ten ukazuje níž).

| Stav | VmRSS |
|---|---:|
| po `/health` (žádný Blazor) | **159 MB** |
| první GET `/` (studený Razor / JIT) | + ~9 MB |
| další GET `/` (prerender, bez živého SignalR) | **+ ~0,7 MB** / request |

Živý SignalR okruh s MudBlazorem je nad prerender hodnotou. I při ~2 MB
na okruh je 30 × 3 min ~60 MB — zlomek 768 MB limitu.

HTTP GET **není** čisté měření živého okruhu (růst pokračoval i přes
`MaxRetained = 10` ve starém image). Na první sezónu stačí; per-page render
mode anonymní provoz z poolu odstraní, ale je to migrace celého Blazor hostu.

Měřicí kontejner `demizon-rss-m` je smazaný. Z předchozí session můžou viset
`demizon-rss` (`127.0.0.1:18085`) a `demizon-cfg` (`127.0.0.1:18086`) — uklidit,
pokud se nepoužívají.

---

## Nové testy (`2d932d0`)

| Soubor | Co hlídá |
|---|---|
| `Demizon.Tests.Unit/AuthApiTests.cs` | HTTP login/refresh, externista, soft-delete, `MemberId`, 401 bez JWT, 403/404 na admin DELETE `/api/events/99999`, profil bez `passwordHash` |
| `Demizon.Tests.Unit/TokenServiceTests.cs` | JWT nese login, roli, `PrimarySid`; odmítne cizí klíč, issuer, expiraci |
| `Demizon.Tests.Unit/ClaimsPrincipalExtensionsTests.cs` | `GetMemberId` čte `PrimarySid` |
| `Demizon.Tests.Unit/Infrastructure/AuthApiFactory.cs` | env overlay, vypnuté hosted services, temp SQLite |

Konvence beze změny: české názvy s podtržítky, holý xUnit `Assert`, žádné
binární fixtures. Podrobnosti v [`testing-plan.md`](testing-plan.md).

---

## Na co nesahej — je hotové

Disk P1–P3 v kódu (ImageSharp, kvóty, purge, WAL, RID publish, Railway
`DATABASE_URL`, odtrackovaný `demizon.sqlite`, DataProtection klíče, env
overlay, měření okruhů). Detaily a naměřené velikosti image jsou v
[`hosting-optimization-plan.md`](hosting-optimization-plan.md).

**Nezačínej další diskovou optimalizaci.** Zbývající položky tam jsou ops
při nasazení, vizuál MudBlazoru a odložená rozhodnutí (doména).

Zablokované na vlastní průchod, ne na tuhle vlnu:

- **Per-page render mode** — anonymní stránky pořád otevírají plný SignalR okruh.
- **`IDbContextFactory`** — lazy loading + scoped kontext na celý Blazor okruh.

---

## Další vlna

### A. Než poleze na veřejnou IP

- [ ] **`POST /api/database/seed` vypnout nebo zamknout.** Je `[AllowAnonymous]`,
      po úspěchu vrátí login `jack` / heslo `admin123`. Podmínka „jen prázdná DB“
      nestačí: první request na čistý volume založí admina, kterého zná kdokoli
      s portem. Buď smazat, nebo vázat na jednorázový env flag, který po seedu
      zmizí. Popis, jak vznikne první admin (ručně / skript), patří k nasazení.
- [ ] **Záloha `/data`.** Fotky jsou BLOB v SQLite, endpoint `GET /api/database/backup`
      je pryč. Bez snapshotu volume na Scalewayu (ideálně i kopie pryč ze stroje)
      je jeden mrtvý disk = konec dat. Napsat konkrétní job, ne větu „řeší infrastruktura“.
- [ ] **VAPID klíče až při nasazení.** Vygenerovat nový pár, předat
      `Vapid__PublicKey` / `Vapid__PrivateKey` / `Vapid__Subject`, z
      `appsettings.Production.json` vymazat. Historii git kvůli nim nepřepisovat —
      dnešní hodnoty nikdy nic nechránily. Rotace produkčního páru odhlásí
      všechny odběratele.
- [ ] **`Jwt__SecretKey` v `docker run`** — bez ní `ValidateOnStart` shodí start.
      Recept je v hosting plánu.
- [ ] **Jednorázový plný `VACUUM`** na produkční SQLite (chce ~2× volného místa).
      Periodický `incremental_vacuum` už běží.
- [ ] **EF globální filtr `Member.DeletedAt` vs. required relace.** Varování při
      startu: `Attendance`, `RefreshToken`, `DeviceToken`, `PushSubscription`.
      INNER JOIN umí po soft-delete „ztratit“ řádky. Matching filtry nebo
      optional navigace. (Stejný druh díry, jakou kdysi měl Include přes
      smazanou entitu.)

`demizon.sqlite` je odtrackovaný, blob s hashi 3 členů v git historii zůstává.
U privátního repa to nehoří; `git filter-repo` jen kdyby repo šlo ven.

### B. Flutter — hlavní produktová práce

Backend pro mobil v zásadě existuje. Děravé je dostání klienta do rukou.
Podrobný seznam je v [`flutter-rewrite-plan.md`](flutter-rewrite-plan.md);
tady pořadí podle dopadu:

- [ ] `flutterfire configure` → `lib/firebase_options.dart` + `google-services.json`
- [ ] Ikony a splash (`flutter_launcher_icons`, `flutter_native_splash`)
- [ ] Běh na **fyzickém telefonu** proti živému API. Na emulátoru je ověřený
      jen login a chybová cesta přihlášení.
- [ ] **Notifikační stack:** FCM registrace tokenu, foreground lokální notifikace,
      deep-linky (cold / background / foreground). V MAUI to byla nejkřehčí část;
      ve Flutteru chybí. Zdroje: `NotificationNavigationService.cs`,
      `NotificationSyncService.cs`, `MainActivity.cs`.
- [ ] DateTime na drátě u zkoušek (`?date=`) — UTC vs. lokální pátek.
- [ ] Auth refresh: 5 min před expirací + fallback na 401.
- [ ] Křížová tabulka docházky (zamrzlý sloupec jmen, swipe vs. vnitřní scroll).
- [ ] Duální režim akce / zkouška na detailu a v editaci.

Až tohle pojede:

- [ ] Vyhodit `Demizon.Maui` z `Demizon.slnx` (blokuje `dotnet test` bez filtru;
      z image ho už `.dockerignore` vylučuje).
- [ ] Pohlídat drift `Demizon.Api` — paralelní host **není** v slnx, stejné
      koncepty/controllery, může se tiše rozejet s Mvc.
- [ ] Offline cache — MAUI ji neměla, není regrese, jen vylepšení k zvážení.

### C. Backend díry, které ublíží až v provozu

- [ ] **Osiřelé události v Google Calendaru u nové docházky.**
      `MemberAttendance.razor.cs`: `model.Id = attendanceResult.Id` je no-op
      (stejná instance), `ToEntity()` vyrábí novou entitu, klíč se do view modelu
      nedostane. U create zůstane `model.Id == 0`, sync nenapíše Google event ID,
      pozdější „nepřijdu“ událost nesmaže. Oprava: `CreateOrUpdateAsync` musí
      vrátit uloženou entitu nebo klíč. Test: vytvořit → uložit ID → smazat.
      Podrobnosti v [`testing-plan.md`](testing-plan.md).
- [ ] **Zahazované `bool` z Core služeb.** 12 metod (`CreateAsync`/`DeleteAsync`
      v Dance, Event, File, Member, VideoLink + Attendance create/update/delete)
      vrací `bool`, služby tracker uklidí, **volající výsledek ignorují** a hlásí
      úspěch. Místa mimo jiné: `ListEvents.razor`, `ListVideoLinks.razor`,
      `ListMembers.razor`, `ListDances.razor`, `AttendancesController` (6×),
      `DancesController`, `FilesController`. Správný tvar je převod na
      `Result` / `Result<T>` z `Demizon.Common`, ne 20 `if`.
- [ ] **MudBlazor 9.9.0 vizuálně.** Build projde, vzhled nikdo neproklikal.
      Stačí telefon + desktop na admin docházce, členech, fotkách, tancích.

### D. Nasazení (až bude doména)

Odložená rozhodnutí z hosting plánu, plus CI image:

- [ ] Doména. Dnes `AllowedHosts` i `GoogleCalendar.RedirectUri` míří na Railway —
      na Scalewayu by každý request skončil HTTP 400.
- [ ] HTTPS přes Caddy (Let's Encrypt), Kestrel jen HTTP na localhost.
      Ne `dotnet dev-certs`.
- [ ] Google OAuth redirect URI 1:1 s Google Cloud Console.
- [ ] Registry: zůstat u Docker Hubu (`jackeq/demizon-mvc`) nebo GHCR.
- [ ] `build.yml` — po testech image `latest` + `sha-<commit>`.
- [ ] `deploy.yml` — `workflow_dispatch` / release, SSH `docker pull` + restart + prune.
- [ ] Na hostiteli `/etc/docker/daemon.json` s rotací logů.
- [ ] Firebase credentials do kontejneru, jinak FCM jen zaloguje warning a mlčí.
- [ ] První admin **ne** přes seed endpoint.

Recept `docker run` (volume, `--memory=768m`, log-opt, `Jwt__SecretKey`) je
v [`hosting-optimization-plan.md`](hosting-optimization-plan.md). Po env overlay
`-e` opravdu vyhraje nad json.

### E. Testy, které záměrně nejsou (nízká priorita)

- [ ] bUnit na Razor (hlavně kvůli MudBlazoru).
- [ ] Dvojník nad `GoogleCalendarService` (dnes volá Google API přímo).
- [ ] Zátěžový test ImageSharp proti stropu 128 MB (`AllocationLimitMegabytes`
      se nastavuje v `AddCoreServices`, unit testy ji nevolají).
- [ ] HTTP 429 na `/api/auth/token` — v test hostu je limit schválně zvednutý.
- [ ] Oddělit `AuthApiTests` do `Demizon.Tests.Web`, ať unit projekt netahe Mvc.

---

## Doporučené pořadí na další 2–3 týdny

1. Zavřít seed endpoint a napsat, jak vznikne první admin. **(A)**
2. Flutter: Firebase + notifikace, ověřit docházku na fyzickém telefonu. **(B)**
3. Opravit Google Calendar ID u create docházky. **(C)**
4. Až bude jasná doména — Caddy, secrets, první `docker run` se snapshotem volume. **(D)**

Nezačínej další diskovou optimalizaci ani per-page render mode.

---

## Příkazy

```bash
dotnet test Demizon.Backend.slnf
dotnet run --project Demizon.Mvc/Demizon.Mvc.csproj
# EF: startup host je Mvc
dotnet ef migrations add <Name> --project Demizon.Dal --startup-project Demizon.Mvc
```

Kontrakty: API vrací `Demizon.Contracts.*`, ne EF entity.
Docházka na drátě: `"yes"|"maybe"|"no"` (`AttendancesController.ParseStatus`).
Zkoušky: attendance s `EventId == null`, páteční sémantika.
Soft-delete členů: globální filtr `Member.DeletedAt == null`.
JWT member id: `ClaimTypes.PrimarySid`, čtení `User.GetMemberId()`.

Commity ve stylu stávající historie: `fix(auth):`, `test:`, `docs(disk):`,
`feat(disk):`. Na `master`, pokud uživatel neřekne jinak.
