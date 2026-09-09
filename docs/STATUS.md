# Stav projektu a plán další práce

> **Živý dokument a vstupní bod.** Průběžně aktualizovat při každé dokončené položce.
> Založeno: 2026-09-08. Poslední aktualizace: 2026-09-09 (nasazení nachystané v nasazeni.md).
>
> Účel: předat kontext další session (Claude / kdokoli) — co je hotové, na co
> nesahat, co zbývá a v jakém pořadí.
>
> **Tohle je jediný dopředný plán.** Featury v [`features/`](features/) říkají,
> co se stalo v nich; „co teď“ je jen tady. Rozcestník: [`README.md`](README.md).

**Větev:** `master` (= `origin/master`). **Host:** `Demizon.Mvc` (Blazor Server + API).
**Aplikace zatím není v produkci.**

| | Stav |
|---|---|
| Testy | **348 zelených** — 126 unit + 169 integration + 53 E2E |
| CI | [`test.yml`](../.github/workflows/test.yml) — dvě úlohy: `Demizon.Backend.slnf` a `Demizon.E2E.slnf` |
| Docker image | 30 MB publish payload, 261 MB image |

Kontrakty, pasti prostředí a příkazy jsou v [`../AGENTS.md`](../AGENTS.md) —
nekopírují se sem, aby nezastaraly na dvou místech.

---

## Uzavřené featury

Podrobnosti, zdůvodnění a naměřená čísla jsou v záznamech; tady jen co to
znamená pro další práci.

| Feature | Záznam | Co z toho platí |
|---|---|---|
| Disková optimalizace pro Stardust | [`disk-optimalizace`](features/disk-optimalizace/README.md) | ImageSharp, kvóty, purge, WAL, RID publish, odtrackovaná dev DB, DataProtection klíče, env overlay, 30 × 3 min okruhy |
| Kvalita: Result, testy, vizuální QA | [`features/kvalita-result-testy/`](features/kvalita-result-testy/README.md) | Result kontrakt napříč službami, E2E sada, validace formulářů, 13 opravených chyb |

### Na co nesahat — je hotové

- **Disk a paměť.** Nezačínej další diskovou optimalizaci. Co zbývá, jsou ops
  kroky při nasazení (viz D).
- **Result kontrakt.** Služby vracejí `Result`/`Result<int>` a volající ho
  kontrolují. Nevracet to na `bool`.
- **Validace dialogových formulářů.** Všechny, které mají povinná pole, mají
  `MudForm` a kontrolu v OK handleru. `AttendanceForm` ji záměrně nemá — nemá
  co validovat.
- **Bootstrap prvního admina.** `POST /api/database/seed` je zamčený za
  `Bootstrap__SeedToken` a po prvním použití se sám vypne. Postup v hosting plánu.

### Zablokované na vlastní průchod

- **Per-page render mode** — anonymní stránky pořád otevírají plný SignalR okruh.
  Je to migrace celého Blazor hostu, ne přepínač. Naměřeno ~1,8 MB na okruh,
  takže to při dnešní velikosti souboru nehoří.
- **`IDbContextFactory`** — lazy loading + kontext scoped na celý okruh.
  Několikadenní refaktor s vysokým rizikem regresí.

---

## A. Než poleze na veřejnou IP

- [x] **Seed endpoint zamčený.** Tři pojistky (404 bez tokenu, 401 na špatný
      token, 409 nad neprázdnou databází), heslo se nevrací a v kódu žádné není.
- [x] **Záloha `/data`** — [`ops/backup-demizon.sh`](../ops/backup-demizon.sh),
      `sqlite3 ".backup"` + `integrity_check` + off-site kopie. Cron a postup
      obnovy (včetně smazání starého WAL/SHM) v hosting plánu.
- [x] **EF globální filtr vs. required relace** — prošetřeno, model se záměrně
      nemění, chování zamykají testy.
- [ ] **VAPID klíče vygenerovat až při nasazení**, mimo repozitář, a předat přes
      `Vapid__*`. Dnešní hodnoty v `appsettings.Production.json` jsou navíc jen
      slepené GUIDy, ne platné P-256 klíče — takže web push zatím nikdy nefungoval.
      Historii kvůli nim není třeba přepisovat. Příkaz v [`nasazeni.md`](nasazeni.md).
- [ ] **Jednorázový plný `VACUUM`** na produkční SQLite po zapnutí
      `auto_vacuum=INCREMENTAL` (chce ~2× volného místa). Periodický
      `incremental_vacuum` už běží. Příkaz v [`nasazeni.md`](nasazeni.md).

---

## B. Flutter — hlavní produktová práce

**Kód, který šel dopsat bez telefonu, je hotový.** Zbývá dostat klienta
do rukou — to bez Firebase konzole a fyzického telefonu nejde.
**Odloženo na později** (2026-09-09).

Podrobný seznam je v [`flutter-prepis/`](features/flutter-prepis/README.md).

### Hotové v kódu

- [x] **Notifikační stack.** Cold / background / foreground + pending replay
      po loginu, reset po odhlášení, foreground přes lokální notifikace
      na kanálu `demizon_channel`. Bez `google-services.json` se FCM tiše
      přeskočí. Zamčeno v `test/notification_navigation_test.dart`.
- [x] DateTime na drátě u zkoušek (`?date=`) — interceptor posílá
      `yyyy-MM-dd`, ne ISO instant. Zamčeno v `test/contract_test.dart`.
- [x] Duální režim akce / zkouška — `EventDetailArgs.fromRoute` a
      `MemberAttendanceTarget.isRehearsal`. Zamčeno testy.
- [x] Auth refresh 5 min + 401 — kód je v `auth_interceptor.dart`.
- [x] Křížová tabulka — zamrzlý sloupec jmen i gesture aréna jsou v kódu.

### Čeká na člověka (Firebase konzole + telefon)

- [ ] `flutterfire configure` → `lib/firebase_options.dart` + `google-services.json`
- [ ] Ikony a splash (`flutter_launcher_icons`, `flutter_native_splash`)
      ze `Demizon.Maui/Resources/AppIcon/` + `demizon_flutter/assets/images/demizon_logo.jpg`
- [ ] Běh na **fyzickém telefonu** proti živému API (přihlášení, docházka,
      ťuknutí na notifikaci, auth refresh, swipe v tabulce). Na emulátoru
      je ověřený jen login a chybová cesta přihlášení.

Až tohle pojede:

- [ ] Vyhodit `Demizon.Maui` z `Demizon.slnx` (blokuje `dotnet test` bez filtru;
      z image ho už `.dockerignore` vylučuje). Ne dřív, než Flutter nahradí
      chování na telefonu — MAUI je pořád zdroj pravdy.
- [ ] Offline cache — MAUI ji neměla, není regrese, jen vylepšení k zvážení.

---

## C. Backend

- [x] **Osiřelé události v Google Calendaru** u nové docházky — opraveno v obou
      směrech (vytvořená událost se po selhaném uložení ruší, ID smazané se
      z databáze nuluje).
- [x] **Zahazované `bool` z Core služeb** — převedeno na `Result`/`Result<T>`,
      všech 35 volajících kontroluje výsledek.
- [x] **Vizuální kontrola MudBlazoru 9.9** — automatizovaná část v E2E,
      ruční proklikání hotové, tři nálezy opravené.
- [x] **Automatické FCM připomínky zkoušky nesly `data: null`.** Tap na
      notifikaci proto nemohl otevřít detail. Teď posílají `rehearsalDate`
      stejně jako ruční připomínka.
- [x] **`Demizon.Api`.** Paralelní host je pryč (`71d9916`). Jediný web host
      je `Demizon.Mvc`. Flutter i Blazor jedou proti němu. Není co dávat do CI
      ani co mazat.
- [ ] **`GoogleCalendarService` sám testovaný není.** Kompenzační logiku
      volajících pokrývá dvojník (`GoogleCalendarCompensationTests`), ale
      vlastní překlad na Google API se testuje jen nepřímo. Nízká priorita:
      je to tenký obal.

---

## D. Nasazení (až bude doména)

**Postup shora dolů je v [`nasazeni.md`](nasazeni.md)** (VAPID, Firebase env,
`VACUUM`, Caddy, seed, cron, CI). Tady jen co zbývá, ať STATUS zůstane
krátký:

- [ ] Doména. Dnes `AllowedHosts` v `appsettings.Production.json` i
      `GoogleCalendar.RedirectUri` míří na Railway — na Scalewayu by každý request
      skončil HTTP 400. (Základní `appsettings.json` už `demizon.cz` obsahuje.)
- [ ] HTTPS přes Caddy (Let's Encrypt), Kestrel jen HTTP na localhost.
      Ne `dotnet dev-certs`.
- [ ] Google OAuth redirect URI 1:1 s Google Cloud Console.
- [ ] Registry: zůstat u Docker Hubu (`jackeq/demizon-mvc`) nebo GHCR.
- [ ] `build.yml` — po testech image `latest` + `sha-<commit>`.
- [ ] `deploy.yml` — `workflow_dispatch` / release, SSH `docker pull` + restart + prune.
- [ ] Na hostiteli `/etc/docker/daemon.json` s rotací logů.
- [ ] Firebase credentials do kontejneru (`FIREBASE_CREDENTIAL_JSON`), jinak FCM
      jen zaloguje warning a mlčí.
- [ ] Naplánovat cron na `ops/backup-demizon.sh` a **vyzkoušet obnovu**, ne jen záloh.

---

## E. Testy

Podrobnosti, konvence a co která sada hlídá: [`testing-plan.md`](testing-plan.md).

- [ ] **Oddělit HTTP testy do `Demizon.Tests.Web`.** `Demizon.Tests.Unit` dnes
      referencuje `Demizon.Mvc` kvůli `WebApplicationFactory` a tahá celý web
      host do outputu. Přibyly `SeedEndpointTests`, `ApiLimitTests`,
      `HttpLocalizationTests` a `GoogleCalendarCompensationTests`, takže je toho
      dost na vlastní projekt.
- [ ] **Zátěžový test paměti** obrazového pipeline proti stropu 128 MB. Chce
      vlastní proces, v testovacím hostu se RSS neizoluje.
- [ ] **bUnit** — nízká priorita, E2E pokrývá, proč byl na seznamu. Zbývá pro
      případ izolované komponenty s vnucenými parametry.

---

## Doporučené pořadí

1. **Flutter na telefonu. (B)** Odloženo. Postup:
   [`flutter-prepis`](features/flutter-prepis/README.md) → *Až budeš mít Firebase a telefon*.
2. **Až bude jasná doména. (D + A)** Postup shora dolů:
   [`nasazeni.md`](nasazeni.md) → *Až budeš nasazovat*. Caddy, secrets, seed,
   VAPID, `VACUUM`, cron záloh + vyzkoušená obnova, CI.

Bez telefonu a bez domény **není co nutně dopsat v kódu.** Položky v E
(oddělit HTTP testy, zátěž paměti, bUnit) a test `GoogleCalendarService`
jsou úklid s nízkou prioritou — nedělej je místo nasazení.

Nezačínej další diskovou optimalizaci ani per-page render mode.
Nevyhazuj `Demizon.Maui` ze slnx, dokud Flutter nepojede na telefonu.

---

## Příkazy

```bash
dotnet test Demizon.Backend.slnf     # unit + integrační (rychlá brána)
dotnet test Demizon.E2E.slnf         # E2E v prohlížeči (chce Chromium)
dotnet run --project Demizon.Mvc/Demizon.Mvc.csproj
# EF: startup host je Mvc
dotnet ef migrations add <Name> --project Demizon.Dal --startup-project Demizon.Mvc
# Flutter (v demizon_flutter/; *.g.dart jsou gitignorované)
flutter pub get && dart run build_runner build && flutter analyze && flutter test
```

Chování aplikace, kontrakty a pasti prostředí: [`../AGENTS.md`](../AGENTS.md).
Commity ve stylu stávající historie (`fix(...)`, `test(...)`, `docs(...)`),
na `master`, pokud uživatel neřekne jinak.
