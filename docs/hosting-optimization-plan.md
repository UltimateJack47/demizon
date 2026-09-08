# Optimalizace backendu a nasazení na Scaleway Stardust

> **Živý dokument.** Průběžně aktualizovat při každé dokončené položce.
> Založeno: 2026-09-01. Poslední aktualizace: 2026-09-08.

## Kontext

Cíl je hostovat `Demizon.Mvc` (Blazor Server + API v jednom hostu) na vlastním serveru
**Scaleway Stardust1-S: 1 vCPU, 1 GB RAM, 10 GB disk**, a napojit na něj mobilní klienta.

Předchozí pokus o nasazení skončil zaplněním disku a kolapsem serveru. Aplikace byla
původně psaná pro **Railway** a nesla si po něm konfiguraci (`/data` volume, parsování
`DATABASE_URL` pro Postgres, doménu v `AllowedHosts`). Parsování `DATABASE_URL` je
odstraněné (Priorita 3), `/data` zůstává — na Scalewayu je to namountovaný volume.
Doména v `AllowedHosts` čeká na rozhodnutí o nasazení.

**Aplikace zatím není v produkci.** Konkrétní parametry nasazení (doména, HTTPS,
Google OAuth redirect) se rozhodnou později — viz sekce *Odložená rozhodnutí*.

---

## Diagnóza

### Proč docházel disk

Build na serveru to nebyl — na server jde jen `docker pull`. Skutečné příčiny:

| # | Příčina | Důkaz |
|---|---|---|
| 1 | `docker run` **bez `-v` pro `/data`** → SQLite DB včetně všech fotek se psala do zapisovatelné vrstvy kontejneru | `Program.cs:34-35` si adresář vytvoří sám (`Directory.CreateDirectory`), takže probe projde a appka pokračuje |
| 2 | `docker run` **bez `--rm`** → zastavené kontejnery se hromadí, každý drží svou vrstvu v `/var/lib/docker/overlay2` | poznámky k nasazení |
| 3 | Opakovaný `docker pull :latest` → staré image zůstávají jako dangling, ~284 MB každý | nikdy se nespouštěl `docker image prune` |
| 4 | `docker logs` bez rotace — výchozí `json-file` driver nemá limit | na hostiteli chybí `/etc/docker/daemon.json` |
| 5 | SQLite nikdy neuvolní smazaná data — v repu **nula** výskytů `VACUUM`/`auto_vacuum` | `Demizon.Dal` |
| 6 | `AuditLog` bez retence, ~200–300 MB/rok (hlavně z refresh tokenů, JWT expirace 60 min) | `AuditSaveChangesInterceptor.cs:21-71`, žádný purge |
| 7 | WAL se nikdy nezkrátí — chybí `journal_size_limit`; navíc `AddDbContext` je v Blazor Serveru scoped na **celý okruh**, takže checkpoint nemůže doběhnout | `DatabaseServiceConfigurationExtension.cs:33,59` |

**Vedlejší efekt bodů 1+2: tichá ztráta dat.** Každý nový kontejner startoval s prázdnou DB.

### Proč docházela paměť

| # | Riziko | Stav |
|---|---|---|
| 1 | **Magick.NET Q16** — 8 B/px, alokace mimo GC haldu, žádné `ResourceLimits` | ✅ **vyřešeno** (viz níže) |
| 2 | Blazor Server circuity bez konfigurace — `ServerPrerendered` dává circuit **i anonymnímu návštěvníkovi**, default `DisconnectedCircuitMaxRetained = 100` × 3 min | ⚠️ **částečně** (retence snížena, viz ✅ 5; per-page render mode zablokovaný) |
| 3 | Server GC zapnutý defaultně, bez heap limitu — nevrací paměť OS | ✅ **vyřešeno** (Workstation GC, viz ✅ 5) |
| 4 | BLOBy v SQLite se načítají celé do paměti, bez streamování | ✅ **vyřešeno** (seznamy jen metadata; `GetContentAsync` tahá jeden sloupec) |

---

## Hotovo

### ✅ 1. Náhrada Magick.NET za ImageSharp

**Změněné soubory:**
- `Demizon.Core/Demizon.Core.csproj` — `Magick.NET-Q16-AnyCPU 14.11.1` → `SixLabors.ImageSharp [3.1.12]`
- `Demizon.Core/Services/FileUpload/FileUploadService.cs` — `OptimizeImage` přepsán, `ResizeAndCreate` odstraněn (měl nula volajících)
- `Demizon.Core/Extensions/CoreServicesRegistrationExtension.cs` — strop alokátoru 128 MB

**Proč ImageSharp:** ImageMagick alokuje pixel buffery **mimo .NET GC haldu**, takže GC
o té paměti neví a nemůže na tlak reagovat — proces prostě narazí do stropu a jádro ho zabije.
ImageSharp alokuje spravovaně. Navíc `DecoderOptions.TargetSize` u JPEGu spustí škálovaný
IDCT, takže se plný raster zdrojové fotky nikdy nealokuje.

**Verze je připnutá zápisem `[3.1.12]`** (hranaté závorky = přesně tato verze). ImageSharp 4.x
vyžaduje při buildu licenční soubor `sixlabors.lic`; řada 3.1.x je čistě Apache 2.0.
Souboru se to týká jen pod 1 M USD ročního obratu — což platí, ale za pin je to jednodušší.

**Naměřeno:**

| Vstup | Magick.NET Q16 | ImageSharp | Poměr |
|---|---:|---:|---|
| 24 Mpx (10,2 MB) — peak RSS | 207 MB | **69 MB** | 3× méně |
| 24 Mpx — čas | ~1 300 ms | **~460 ms** | 2,8× rychleji |
| 100 Mpx (43 MB) — peak RSS | **710 MB** | **104 MB** | **7× méně** |
| 100 Mpx — čas | 13 000 ms | **2 000 ms** | 6,5× rychleji |
| Velikost `dotnet publish` | 284 MB | **60 MB** | −224 MB (−79 %) |

Těch 224 MB byly nativní knihovny `Magick.Native` pro **všechny** platformy
(linux-x64, linux-musl, linux-arm64, osx-x64, osx-arm64, win-x64/x86/arm64), protože
varianta `-AnyCPU` je veze všechny. Šly přímo do Docker image.

**Změna chování, kterou je třeba znát:** nový kód volá `AutoOrient()` **před** zahozením
metadat. Původní kód volal `Strip()`, ale rotaci nikdy neaplikoval do pixelů, takže fotky
z mobilu na výšku se zobrazovaly položené. Nově nahrané fotky budou správně; **už uložené
to zpětně neopraví**.

Druhá odchylka: výstupní JPEGy jsou při stejné nominální kvalitě 80 o něco větší
(155 kB vs 129 kB) — enkodéry se liší v tom, co „quality 80" znamená. Případně snížit
`JpegQuality` na 75.

### ✅ 2. Oprava Razor kompilace, která blokovala `dotnet publish`

`Demizon.Mvc/Demizon.Mvc.csproj:50` mělo `<UseRazorSourceGenerator>false</UseRazorSourceGenerator>`,
což nutilo build do starého samostatného `rzc` kompilátoru (v příkazové řádce `-c MVC-3.0`).
Ten na tomto kódu padal:

```
Nullable object must have a value.
   at Microsoft.AspNetCore.Razor.Language.Components.ComponentNodeWriter.WriteComponentAttributeName(...)
   → rzc generate exited with code 1
```

**Byla to chyba existující už před ostatními změnami** (ověřeno buildem původního kódu).
Protože `Dockerfile:22` dělá `dotnet publish -c Release`, padal na tomhle i build image.
Přepnuto na `true` (moderní source generator je dnes výchozí a udržovaný).

> Pozor: Razor generace je inkrementální podle časových razítek `.razor` souborů, ne podle
> reference setu. Debug build proto může „projít" ze staré cache. Jediný důvěryhodný test
> je `dotnet publish -c Release` do čistého adresáře.

### ✅ 3. Aktualizace NuGet balíčků

Zranitelnost **`SQLitePCLRaw.lib.e_sqlite3 2.1.11`** ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q),
severity High) vyřešena tranzitivně přes EF Core 10.0.11 → `SQLitePCLRaw 2.1.12`.
Přímá reference nebyla potřeba. `dotnet list package --vulnerable` je nyní čistý.

| Balíček | Z | Na | Poznámka |
|---|---|---|---|
| Microsoft.EntityFrameworkCore (+ .Design, .Proxies, .Sqlite) | 10.0.5 | 10.0.11 | patch |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.5 | 10.0.11 | patch |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.5 | 10.0.11 | patch |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.5 | 10.0.11 | patch |
| Google.Apis.Auth | 1.73.0 | 1.76.0 | minor |
| Google.Apis.Calendar.v3 | 1.73.0.4073 | 1.75.0.4206 | minor |
| System.IdentityModel.Tokens.Jwt | 8.9.0 | 8.22.0 | minor |
| FirebaseAdmin | 3.1.0 | 3.6.0 | minor |
| MudBlazor | 9.3.0 | 9.9.0 | minor — **build projde, ale vizuální změny nejsou otestované** |
| WebPush | 1.0.12 | 1.0.13 | patch |
| **CryptoHelper** | 4.0.0 | 5.1.0 | **major, viz níže** |

**Záměrně neaktualizováno:**
- `SixLabors.ImageSharp` zůstává na `[3.1.12]` — 4.x vyžaduje licenční soubor.
- `Append.Blazor.Notifications 1.1.0` — už je aktuální.

#### CryptoHelper 4.0.0 → 5.1.0 — co bylo ověřeno

Dvě breaking změny:
1. Třída `Crypto` přejmenována na `PasswordHasher`. Celkem **9 volání v 6 souborech**:
   `RefreshTokenService.cs:32,64`, `AuthController.cs:21,28`, `DatabaseController.cs:44`
   a — doplněno až později, viz bod 6 — `AuthenticationService.cs:21,27,81`,
   `MembersController.cs:45,51`, `MemberViewModel.cs:78`.
2. „Security hardening" v v5.0.0 přidalo **validační limity při ověřování**:
   `MinimumIterCount = 10_000`, `MaxSaltLength = 64 B`, odmítání slabých PRF.
   Hash s parametry pod těmito limity se **neověří** → uživatel se nepřihlásí.

Formát hashe se nemění (ASP.NET Identity V3, parametry jsou v hlavičce hashe).
Dekódování hlaviček v lokální DB ukázalo:

```
marker=V3  PRF=HMACSHA256  iterace=600 000  saltlen=16 B  → 3 z 3 záznamů
```

To je bezpečně nad všemi novými limity. ⚠️ **Ověřit stejným způsobem i produkční databázi,
až vznikne** — hashe vytvořené verzemi před CryptoHelper 4.0.0 mohly mít nižší počet iterací
a po nasazení by přestaly fungovat (projeví se jako „špatné heslo", řeší se resetem hesla).

### ✅ 4. Strop alokátoru ImageSharpu

`CoreServicesRegistrationExtension.cs` — `AllocationLimitMegabytes = 128`.
Pojistka pro případ, že by dorazil obrázek, na který nestačí ani škálovaný dekód;
místo vyčerpání paměti kontejneru skončí zpracování výjimkou.

Ověřeno, že limit nerozbíjí běžný provoz: 24 Mpx fotka projde na 69 MB RSS,
100 Mpx na 104 MB RSS, obě produkují platný výstup.

---

### ✅ 6. Testovací infrastruktura a opravy, které z ní vypadly

Solution neměla ani jeden testovací projekt. Přidané: `Demizon.Tests.Unit` (76 testů)
a `Demizon.Tests.Integration` (155 testů), plus `Demizon.Backend.slnf`, protože
`dotnet test Demizon.slnx` neprojde — `Demizon.Maui` chce workload `maui-android`.
Podrobnosti a plán dalších vrstev: **`docs/testing-plan.md`**.

Testy odhalily tři chyby, které build ani ruční proklikání nezachytí:

1. **`TargetSize` v ImageSharpu není strop na šířku, ale bounding box bez stropu na
   faktoru 1.0.** Fotky na výšku vycházely užší než 1200 px (3000×4000 → 900×1200,
   1000×5000 → 240×1200) a malé fotky se naopak **zvětšovaly** (800×600 → 1200×900).
   Regres vznikl právě při náhradě Magick.NETu. Opraveno v `ComputeDecodeSize`,
   škálovaný IDCT a s ním paměťová výhoda zůstaly.
2. **Audit log nesl u vložených entit dočasný primární klíč** (`-2147482632`), protože
   `SavingChangesAsync` běží před uložením. Žádný záznam `Added` nešel spárovat s řádkem.
3. **Audit log měl pro tutéž entitu dva různé názvy typu** — `entry.Entity.GetType().Name`
   vrací s `UseLazyLoadingProxies()` `"MemberProxy"`. Opraveno na `entry.Metadata.ClrType.Name`.

Dále dokončena **migrace CryptoHelper 4 → 5**: v branchi byly přepsané 3 z 9 volání.
Zbylých 6 ve třech souborech (`AuthenticationService.cs` — webové přihlášení,
`MembersController.cs` — změna hesla, `MemberViewModel.cs` — zakládání členů)
jelo dál přes obsolete `Crypto`. Chování bylo stejné (shim nad `PasswordHasher`),
ale s příštím major updatem by se to rozpadlo.

> `dotnet publish -c Release` už nehlásí **žádné** `CS0618` z CryptoHelperu. Jedno
> `CS0618` ale zbývá, a to nové, zavlečené bumpem FirebaseAdmin 3.1.0 → 3.6.0:
> `FcmService.cs:71` — `Message.Token` je zastaralé ve prospěch `Fid`. Ověřeno, že
> `Token` v 3.6.0 stále funguje (nese `[JsonProperty("token")]` a projde validací),
> takže push notifikace rozbité nejsou. **Záměrně neměněno** — `Fid` je Firebase
> Installation ID, což není totéž co registrační token, takže mechanické přejmenování
> by bylo chybné. Vyžaduje vlastní průchod, viz Priorita 3.

---

### ✅ 5. Priorita 1 — paměť (částečně)

- [x] **Blazor Server circuity.** `Program.cs` — `AddServerSideBlazor` nakonfigurováno na
      `DisconnectedCircuitMaxRetained = 10` a `DisconnectedCircuitRetentionPeriod = 1 min`
      (default bylo 100 okruhů × 3 min). `DetailedErrors` jen ve vývoji.
- [x] **Workstation GC** v `Demizon.Mvc.csproj` — `ServerGarbageCollection=false`,
      `ConcurrentGarbageCollection=false`.
- [x] **SQLite pragmas** (předsunuto z Priority 2, protože nahrazuje zablokovaný
      `AddDbContextFactory`). `SqliteBusyTimeoutInterceptor` nyní aplikuje i
      `journal_size_limit=32 MB` a `wal_autocheckpoint=512`. Ověřeno proti reálné DB:
      všechny tři pragmy se propíšou.

#### ⚠️ Ladění, které vyplynulo z code review — rozhodnuto 2026-09-02

Dvě hodnoty z bodu 5 vypadají jako řešení, ale při bližším pohledu jimi nejsou.
Obojí je kompromis nad paměťovým budgetem, ne chyba.

> **Rozhodnutí:** obě hodnoty **zůstávají jak jsou**. U okruhů proto, že ladit je bez
> měření by znamenalo vyměnit jeden odhad za druhý — viz nový úkol v Prioritě 2.
> U WAL pragem proto, že `journal_size_limit` je neškodná pojistka a častější menší
> checkpointy mají větší šanci proklouznout mezi čtenáři; jen se přestávají vydávat
> za vyřešený problém.

**a) `DisconnectedCircuitMaxRetained = 10` je pravděpodobně příliš málo.**
Protože `_Host.cshtml` dává circuit i anonymnímu návštěvníkovi (viz blok níže), padají
veřejné návštěvy a přihlášení adminové do **stejného** poolu. Pár náhodných návštěv
veřejné stránky tedy vytlačí adminův odpojený okruh během sekund, a s retencí 1 minuta
stačí, aby adminovi zhasla obrazovka telefonu nad rozepsaným formulářem docházky
a po návratu dostal „reconnection failed“ a přišel o rozepsaná data.

| Varianta | Paměť navíc (odhad 1–3 MB/okruh) | Riziko ztráty rozepsané práce |
|---|---:|---|
| dnes: 10 × 1 min | ~10–30 MB | vysoké |
| 30 × 1 min | ~30–90 MB | střední |
| 30 × 3 min (default retence) | ~30–90 MB, drženo 3× dél | nízké |
| default: 100 × 3 min | ~100–300 MB | nízké, ale na 1 GB nereálné |

Odhad na okruh je nutné **naměřit**, ne hádat. Skutečné řešení je per-page render mode,
který anonymní provoz z poolu odstraní úplně — do té doby je to volba mezi RAM a UX.

**b) `wal_autocheckpoint=512` a `journal_size_limit=32 MB` spolu WAL neomezí.**
Ověřeno na reálné DB: `page_size = 4096`, takže 512 stránek = **2 MB**. Autocheckpoint
tedy spouští checkpoint už na 2 MB a limit 32 MB je za normálního provozu nedosažitelný —
uplatnil by se jen při checkpointu, který WAL resetuje. A přesně to podle diagnózy
(řádek 7) nejde: `AddDbContext` je scoped na celý Blazor okruh, takže čtenáři drží
snapshoty a checkpoint nedoběhne.

Ve scénáři, na který ta opatření míří (hromadný zápis při otevřených okruzích), tedy
WAL roste dál. Snížení autocheckpointu z 1000 na 512 navíc **zdvojnásobuje počet
pokusů** o checkpoint proti stejné kontenci, se kterou se potýká `busy_timeout`.

`journal_size_limit` je neškodná pojistka a zůstává. Co WAL skutečně zastropuje,
je periodický `wal_checkpoint(TRUNCATE)` z Priority 2 — **do té doby se řádek 7
v diagnóze nepovažuje za vyřešený.** `wal_autocheckpoint=512` taky zůstává:
častější menší checkpointy mají větší šanci proklouznout mezi čtenáři než vzácné
velké, ale je to argument bez měření, takže se s tou hodnotou nemá hýbat naslepo
ani jedním směrem.

#### ⛔ Zablokováno — `AddDbContextFactory`

**Není to bezpečná výměna.** Entity mají `virtual` navigační vlastnosti napříč celým
modelem (`Attendance.cs:31,35`, `Event.cs:32`, `Member.cs:35,37,39`, `Dance.cs:19,21` …)
a `DatabaseServiceConfigurationExtension.cs:17` zapíná `UseLazyLoadingProxies()`.
Kdyby služby vytvářely krátkodobé kontexty a vracely entity, každý přístup k navigační
vlastnosti po dispose by hodil `ObjectDisposedException`.

Refaktor by tedy znamenal: převést všech 10 služeb v `Demizon.Core/Services/` na
`IDbContextFactory<DemizonContext>`, **vypnout lazy loading** a projít každé místo,
kde se spoléhá na líné načtení navigace, a nahradit ho explicitním `Include()`.
To je několikadenní práce s vysokým rizikem regresí — chce vlastní průchod a testování.

Dobrá zpráva: Razor komponenty `DemizonContext` neinjektují vůbec (ověřeno grepem),
jdou přes služby. Refaktor se tedy odehraje čistě v `Demizon.Core`, ne v UI.

#### ⛔ Zablokováno — veřejné stránky bez circuitu

`Pages/_Host.cshtml:8` používá předosmičkový hostingový model: jediná direktiva
`<component type="typeof(App)" render-mode="ServerPrerendered" />` platí pro celou
aplikaci. Per-stránkový render mode vyžaduje migraci na .NET 8+ model —
`MapRazorComponents<App>()` a `@rendermode` na jednotlivých komponentách.
To je architektonická změna celého Blazor hostu, ne konfigurační přepínač.

Dopad, dokud se to nevyřeší: každá anonymní návštěva veřejné stránky
(`Index`, `Photos`, `Dances`, `PrivacyPolicy`, `TermsOfService` — 5 z 19 stránek
a zdaleka nejvíc trafficu) otevře plnohodnotný SignalR okruh. Snížená retence
odpojených okruhů (výše) to zmírňuje, neodstraňuje.

---

## TODO

### Priorita 2 — disk

> Aktualizace 2026-09-05: Priority 2 implementace v PR #5 (`feat/stardust-disk-optimization`).
> Zůstává: naměření RSS okruhů; jednorázový ops `VACUUM` při nasazení.

- [ ] **Naměřit skutečnou paměť na jeden odpojený Blazor okruh** a podle toho nastavit
      `DisconnectedCircuitMaxRetained`. Dnešní hodnota 10 je zvolená konzervativně,
      odhad 1–3 MB/okruh je nepodložený. Bez měření je volba mezi RAM a rizikem, že
      admin přijde o rozepsaný formulář docházky, jen výměna jednoho odhadu za druhý.
      Postup: v kontejneru s `--memory=768m` otevřít N okruhů, odpojit je a odečíst
      RSS před a po. Skutečné řešení zůstává per-page render mode, který anonymní
      provoz z poolu odstraní úplně.
- [x] **Purge job** pro `AuditLog` (retence 90 dní), `RefreshTokens` (revokované + expirované),
      `SentNotifications` (180 dní). Nejlépe do `UnifiedNotificationService.RunCheckAsync`,
      která už běží 1×/hod.
- [x] **Whitelist entit v auditu** — `AuditSaveChangesInterceptor.cs`. Dnes se auditují
      i `RefreshToken`, `SentNotification`, `DeviceToken`, `File`, které tvoří ~90 % objemu
      a nemají auditní hodnotu. **Priorita stoupla:** oprava dočasných primárních klíčů
      (viz *testing-plan.md*) přidává u každého vložení jedno UPDATE kolečko a nejčastější
      insert je právě `RefreshToken` — po whitelistu extra zápis skoro zmizí.
- [x] **Index na `AuditLog.Timestamp`** — jinak bude i purge full scan.
- [x] **`auto_vacuum=INCREMENTAL`** + periodický `wal_checkpoint(TRUNCATE)` / `incremental_vacuum` přes `DiskMaintenanceHostedService`.
      Jednorázový plný `VACUUM` na produkční DB stále jako ops krok (potřebuje ~2× volného místa).
- [x] **Kvóta na uploady.** Dnes limit 25 MB/soubor a **žádný** limit na počet ani celkový objem.
      Dokumenty se navíc neoptimalizují vůbec (`FileUploadService.cs:70-87` ukládá raw bajty).
      400 PDF × 25 MB = 10 GB = plný disk.
- [x] **`GET /api/database/backup` odstraněn** — zálohy DB řeší Scaleway infrastructure
      tools, endpoint v appce zbytečně tahal celou SQLite (včetně fotek) do RAM/tmp.

### Priorita 3 — úklid a hygiena

- [x] **Mrtvý kód odstraněn:** `UploadImageAsync` + jeho deklarace v `IFileUploadService`
      (filesystémová varianta uploadu, nula volajících — všechna tři místa v UI i oba API
      endpointy jdou přes `UploadImageToDbAsync` / `UploadDocumentToDbAsync`),
      `UploadSettings.Resize` i celá třída `ResizeSettings`, `UploadSettings.ImagesDirectory`
      (čteno jen zrušenou metodou) a `UploadSettings.AllowedFileExtensions`,
      `AttendanceReminderBackgroundService.cs` a `NotificationHostedService.cs`
      (nikde neregistrované — jediné dvě `AddHostedService` v repu jsou
      `UnifiedNotificationService` a `DiskMaintenanceHostedService`),
      `docker-entrypoint.sh`.
      > `AllowedFileExtensions` nebyl bezpečnostní kontrolou, kterou by odstranění vypnulo:
      > sekce `Upload` v appsettings **neexistuje** v žádném prostředí, takže to pole bylo
      > vždy prázdný `List`. Přípony se reálně validují v `DancesController` proti vlastní
      > konstantě `AllowedDocumentExtensions`; u obrázků je kontrolou samotné dekódování
      > ImageSharpem a re-enkód na JPEG, takže nevalidní vstup neprojde.
      >
      > Zbývá 7 ze 42 endpointů v `Demizon.Maui/Services/IApiClient.cs` — neřešeno záměrně,
      > `Demizon.Maui` je nahrazován Flutter klientem a `.dockerignore` ho z image vylučuje.
- [ ] **VAPID privátní klíč je commitnutý v gitu** (`appsettings.Production.json:17`).
      Vygenerovat nové klíče a předávat přes proměnné prostředí. **Vyžaduje ruční krok** —
      nové klíče musí vzniknout mimo repozitář a uložit se do secrets.
- [x] **`demizon.sqlite` odtrackován** (`git rm --cached`), přidán do `.gitignore` a z
      `Demizon.Mvc.csproj` odstraněn blok `CopyToOutputDirectory=Always`.
      Kopie do outputu byla čistá zátěž: lokálně jde `Data Source=demizon.sqlite` proti
      pracovnímu adresáři projektu (`Program.cs` má `SetBasePath(Directory.GetCurrentDirectory())`),
      ne proti `bin/`, a v produkci je cesta absolutní (`/data/demizon.sqlite`). Ta kopie
      tedy nikdy nikoho neobsluhovala — jen vezla 450 kB dev databáze do image, včetně
      3 záznamů členů s hashi hesel.
      > ⚠️ `git rm --cached` soubor **nemaže z historie**. Blob v ní zůstává dohledatelný;
      > pokud je to problém, chce to `git filter-repo` / BFG a force push — samostatné
      > rozhodnutí, ne součást téhle branche. Stejná úvaha platí pro VAPID klíč výše.
- [x] **`.dockerignore`** doplnit: `graft/`, `docs/`, `Demizon.Maui/`,
      `**/appsettings.Local.json`, `**/*.sqlite*`.
- [x] **`Dockerfile:18`** — duplicitní `dotnet build` odstraněn.
- [x] **Publish s `-r linux-x64`** — `Dockerfile` publikuje s
      `-r linux-x64 --self-contained false`. Naměřeno na skutečném publish outputu:

      | Varianta | Velikost | Souborů |
      |---|---:|---:|
      | `dotnet publish` (bez RID) | 62 MB | 143 |
      | `-r linux-x64 --self-contained false` | **30 MB** | 120 |
      | totéž + `PublishReadyToRun=true` | 56 MB | 120 |

      Zmizel celý adresář `runtimes/` — 22 kopií `libe_sqlite3` pro cíle jako `linux-mips64`,
      `linux-musl-s390x` nebo `maccatalyst-arm64`. Ta jediná potřebná (`libe_sqlite3.so`)
      se zkopírovala naplocho do rootu publish outputu, takže SQLite dál funguje.
      `dotnet restore` v Dockerfile dostal `-r linux-x64` taky, jinak by publish restore
      opakoval a rozbil cache vrstvy.

      **Ověřeno na hotovém image (2026-09-08):** `docker build` prošel, výsledný image
      **261 MB** proti 509 MB předchozího (−248 MB, −49 %; většinu z toho udělala už
      náhrada Magick.NET → ImageSharp, `-r linux-x64` přidalo zbytek). V kontejneru
      `/app/runtimes` neexistuje, `libe_sqlite3.so` (1,4 MB) leží v `/app` a
      `/app/demizon.sqlite` tam po odtrackování dev DB **není**. Kontejner spuštěný
      s `--memory=768m` odpověděl na `/health`
      `{"status":"Healthy","checks":[{"name":"database","status":"Healthy"}]}`, takže
      SQLite přes nativní knihovnu reálně jede; veřejná homepage vrátila HTTP 200
      (29,7 kB). `/data` obsahuje `demizon.sqlite` + WAL a adresář `keys/` s vygenerovaným
      DataProtection klíčem. **RSS v klidu po jednom anonymním načtení stránky: 81 MB
      z 768 MB.** To je zatím nejbližší reálné číslo k paměťovému budgetu, ale *není*
      to měření okruhu — na to je pořád potřeba otevřít a odpojit N okruhů (viz Priorita 2).

      **`PublishReadyToRun` záměrně nezapnuto.** Vrátil by 26 MB z ušetřených 32 (+87 %
      proti RID variantě), protože předkompiluje i EF Core, MudBlazor a ImageSharp, ne jen
      vlastní kód. Na 10 GB disku, který je tady tvrdý limit, je to špatný obchod za
      rychlejší cold start; k přehodnocení, kdyby se startovací čas na 1 vCPU ukázal jako
      reálný problém. Trimming ani AOT s EF Core + Blazor nejde.
- [x] **Railway zbytky v `Program.cs`** — parsování `DATABASE_URL` pro Postgres odstraněno.
      Byl to mrtvý kód se zubem: aktivoval se jen v produkci při nastavené `DATABASE_URL`
      a tichým přepsáním `ConnectionStrings:Default` na Postgres syntaxi by shodil SQLite
      připojení. Probe `/data` zkrácen na 5 s, WAL retry na 3×1 s (2026-09-08).
- [x] **DataProtection klíče** — `PersistKeysToFileSystem` (`/data/keys` v produkci,
      `dp-keys/` lokálně, gitignore). Bez toho každý restart shodil auth cookies.
- [ ] **Otestovat MudBlazor 9.9.0 vizuálně** — build projde, ale změny vzhledu build nezachytí.

---

## Odložená rozhodnutí

Appka není v produkci, tyto věci se rozhodnou později:

- [ ] **Doména a HTTPS.** Dnes `appsettings.Production.json:8` má
      `AllowedHosts: "demizon-production.up.railway.app;localhost"` — na Scalewayu by
      **každý request skončil HTTP 400**, protože hlavička `Host` v seznamu není.
      Stejně tak `GoogleCalendar.RedirectUri` (`:13`) míří na Railway.
- [ ] **HTTPS řešit reverzní proxy**, ne `dotnet dev-certs` (dev certifikát prohlížeč
      na veřejné doméně odmítne). Caddy stačí takto:
      ```
      demizon.cz {
          reverse_proxy 127.0.0.1:8083
      }
      ```
      Let's Encrypt si vyřídí sám. Kestrel pak jede prostý HTTP na localhostu.
- [ ] **Google OAuth redirect URI** musí přesně odpovídat tomu, co je zaregistrované
      v Google Cloud Console.

### Správný `docker run` (až se bude nasazovat)

```bash
docker volume create demizon-data      # jednou

docker pull <image>:latest
docker stop demizon 2>/dev/null; docker rm demizon 2>/dev/null
docker run -d --name demizon --restart unless-stopped \
  -p 127.0.0.1:8083:8080 \
  -v demizon-data:/data \
  -e ASPNETCORE_URLS="http://+:8080" \
  -e AllowedHosts="<domena>" \
  -e Jwt__SecretKey="<nahodny retezec, min. 32 znaku>" \
  --memory=768m --memory-swap=768m \
  --log-opt max-size=10m --log-opt max-file=3 \
  <image>:latest
docker image prune -f
```

Na co si dát pozor:
- **Přepínače patří PŘED jméno image.** V `docker run [OPTIONS] IMAGE [COMMAND]` je
  všechno za jménem image příkaz pro kontejner. Původní zápis
  `docker run -p 8083:8080 image -e ASPNETCORE_URLS=...` proměnnou nikdy nenastavil.
- **`-p 127.0.0.1:8083:8080`**, ne `-p 8083:8080` — Docker si píše vlastní pravidla
  do iptables a obchází tím UFW; bez prefixu je port otevřený do internetu.
- **`--memory=768m`** je lepší páka než `DOTNET_GCHeapHardLimit` — .NET čte cgroup limit
  a sám si nastaví heap hard limit na ~75 % z něj.
- **`Jwt__SecretKey` je povinná, jinak kontejner nenastartuje.** Ověřeno smoke testem:
  bez ní `ValidateOnStart` shodí start s `OptionsValidationException: DataAnnotation
  validation failed for 'JwtSettings' members: 'SecretKey'`. Sekce `Jwt` není v žádném
  `appsettings.json`, což je správně (je to tajemství), ale recept ji musí předat.
  Dvojité podtržítko je oddělovač sekcí — `Jwt__SecretKey` = `Jwt:SecretKey`.
  `Issuer` a `Audience` mají v `JwtSettings` výchozí hodnoty, takže je předávat netřeba;
  s jedinou `Jwt__SecretKey` naběhne appka do `Healthy`. Firebase je volitelná (bez ní
  jen varování a vypnuté FCM push). `Vapid__*` a `GoogleCalendar__RedirectUri` jsou
  povinné, ale dnes jsou v `appsettings.Production.json` — až se VAPID klíče přesunou
  do secrets (Priorita 3), přidají se sem taky.

Na hostiteli ještě `/etc/docker/daemon.json`:
```json
{ "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
```

---

## Plán CI/CD (GitHub Actions)

Repozitář: `github.com/UltimateJack47/demizon`.

**Cena:** veřejné repo = neomezené minuty zdarma. Privátní repo na plánu Free =
**2 000 Linux minut měsíčně**. Build .NET image trvá ~3–5 min, takže i privátně
vychází ~400 buildů měsíčně zdarma. Pro tenhle projekt bohatě stačí.

**Navržený tvar:**

1. **`build.yml`** — trigger `push` do `master`. Nejdřív `dotnet test Demizon.Backend.slnf`
   (231 testů, ~6 s), pak Docker image do registry se dvěma tagy: `latest` a `sha-<commit>`.
   Pozor: **`Demizon.slnx` v CI stavět nelze**, `Demizon.Maui` vyžaduje workload
   `maui-android` — proto solution filter.
2. **`deploy.yml`** — trigger `workflow_dispatch` (ruční spuštění) nebo `release`.
   Přes SSH na server udělá `docker pull` + restart kontejneru + `docker image prune -f`.

Rozdělení na dva workflow je záměrné: build po každém pushi, nasazení jen když to chceš.

**Registry:** dnes se používá Docker Hub (`jackeq/demizon-mvc`). Free plán tam dává
1 privátní repozitář, což stačí. Alternativa GHCR (`ghcr.io`) je těsněji integrovaná
s Actions (autentizace přes `GITHUB_TOKEN`, není potřeba spravovat heslo), ale u privátních
balíčků platí kvóta GitHub Packages (500 MB storage na Free plánu) — image má dnes ~60 MB,
takže by se vešlo jen pár tagů. Doporučení: **zůstat u Docker Hubu**, nebo na GHCR mazat
staré tagy.

**Tajemství** (`Settings → Secrets and variables → Actions`): přihlašovací údaje do registry,
SSH klíč na server, VAPID klíče, Firebase service account, Google OAuth. Do image nepatří
nic z toho — všechno se předá jako `-e` při `docker run`.

⚠️ Zatím **nezaloženo**, čeká na rozhodnutí o doméně a registry.
