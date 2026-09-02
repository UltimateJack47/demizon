# Testovací strategie

> **Živý dokument.** Průběžně aktualizovat při každé dokončené položce.
> Založeno: 2026-09-02. Poslední aktualizace: 2026-09-02.

## Kontext

Solution do teď neměla **ani jeden** testovací projekt. Přitom se v ní odehrály dvě
rizikové změny — přechod z Magick.NETu na ImageSharp a major update CryptoHelperu —
u kterých build ani ručním proklikáním nezachytí, že se změnilo chování.

Cíl proto není procentní pokrytí, ale **zamknout kontrakty, jejichž tichá regrese
je drahá**: rozměry ukládaných fotek, formát stavu docházky, soft delete členů,
neprosakování hashů do auditu a jednorázovost refresh tokenů.

---

## Struktura

| Projekt | Co testuje | Rychlost |
|---|---|---|
| `Demizon.Tests.Unit` | Čistá logika bez I/O — mapování na DTO, kontrakt docházky, obrazový pipeline, `Result` | ~3 s / 72 testů |
| `Demizon.Tests.Integration` | Chování nad **skutečnou SQLite** — služby, interceptory, EF model, migrace | ~3 s / 111 testů |

### Proč skutečná SQLite a ne EF InMemory

Testovaný kód se opírá o relační chování, které InMemory provider nemá:

- `RefreshTokenService.CreateAsync` používá `BeginTransactionAsync`
- `RefreshTokenService.ValidateAsync` používá `ExecuteUpdateAsync` (podmíněný UPDATE)
- `SqliteBusyTimeoutInterceptor` nastavuje PRAGMA
- unique indexy, FK kaskády a `HasDefaultValue` InMemory neuplatňuje

`DatabaseFixture` proto drží otevřené in-memory SQLite spojení po dobu života testu
(in-memory databáze zmizí se zavřením posledního spojení) a schéma staví přes
`EnsureCreated`. Testy PRAGMA jedou nad **souborovou** databází, protože WAL se
v in-memory chová jinak.

### Jak spouštět

```bash
dotnet test Demizon.Backend.slnf          # oba projekty
dotnet test Demizon.Tests.Unit            # jen rychlá logika
```

> ⚠️ **`dotnet test Demizon.sln` neprojde.** `Demizon.Maui` vyžaduje workload
> `maui-android`, který na běžném stroji ani v CI není. Proto je v repu
> `Demizon.Backend.slnf` — solution filter se všemi projekty kromě MAUI.
> (MAUI se navíc přepisuje do Flutteru na samostatné branchi.)

---

## Nalezené chyby

Tři chyby, které testy odhalily a které nešly vidět čtením kódu ani buildem.

### ✅ 1. Obrazový pipeline zužoval fotky na výšku a zvětšoval malé

`DecoderOptions.TargetSize` se v ImageSharpu vyhodnocuje jako `ResizeMode.Max`, tedy
jako **bounding box bez stropu na faktoru 1.0**. Předaný čtverec `1200×1200` tím pádem:

| Vstup | Kontrakt (Magick) | Chybné chování | Po opravě |
|---|---|---|---|
| 4000×3000 | 1200×900 | 1200×900 | 1200×900 |
| 3000×4000 | 1200×1600 | **900×1200** | 1200×1600 |
| 1000×5000 | 1000×5000 | **240×1200** | 1000×5000 |
| 800×600 | 800×600 | **1200×900** (upscale) | 800×600 |

Vedlejší důsledek: podmínka `if (image.Width > maxWidth)` v `ResizeToWidth` byla pro
plnou variantu **vždy false**, protože `TargetSize` obrázek zmenšil už při dekódování.
Kód vypadal, že šířku vynucuje, ale nevynucoval.

**Oprava** (`FileUploadService.ComputeDecodeSize`): box se počítá z poměru stran
zdroje a faktor se zastropuje na `1.0`. Škálovaný IDCT tím zůstal zachovaný, takže
paměťová výhoda proti Magicku platí dál. Navíc se strop správně vztahuje na
**zobrazenou** šířku — u EXIF orientace 5–8 je to uložená *výška*.

### ✅ 2. Audit log nesl u vložených entit dočasný primární klíč

`AuditSaveChangesInterceptor.SavingChangesAsync` běží **před** uložením, takže klíč
vkládané entity je v tu chvíli jen placeholder, který EF generuje jako záporné číslo
(`-2147482632`). Každý audit záznam s akcí `Added` tedy nešel spárovat s řádkem,
který popisuje.

**Oprava:** klíč se u vkládaných entit (`PropertyEntry.IsTemporary`) doplní
v `SavedChangesAsync`, kdy už ho databáze přiřadila.

> **Cena:** u uložení, které něco vkládá, přidává jedno UPDATE kolečko.
> Nejčastější insert je dnes `RefreshToken` (každé přihlášení i obnova tokenu) —
> a ten má podle *hosting-optimization-plan.md* z auditu úplně vypadnout
> (whitelist entit, Priorita 2). Po té změně bude extra zápis vzácný.

### ✅ 3. Audit log měl pro tutéž entitu dva různé názvy typu

`entry.Entity.GetType().Name` vrací s `UseLazyLoadingProxies()` název **proxy typu**.
Entita načtená z DB se proto auditovala jako `"MemberProxy"`, ale nově vložená jako
`"Member"` — v audit tabulce se nedalo filtrovat podle typu.

**Oprava:** `entry.Metadata.ClrType.Name` (typ z EF modelu, ne runtime typ instance).

---

## Poučení z code review

Dvě kola review nad tímto PR. Kolo 1 našlo dvě středně závažné chyby, kolo 2 pak
**dvě vysoce závažné, které způsobila oprava z kola 1** — a to je hlavní poučení:

> Změna kontraktu z „vyhodí výjimku“ na „vrátí neúspěch“ odstraní záchytnou síť
> `catch (Exception)` u **všech** volajících naráz. Nestačí zkontrolovat, že volající
> návratovou hodnotu testují; je nutné projít i to, co dělají, když je test negativní,
> a co hlásí uživateli **po** smyčce.

`FileUploadService.UploadImageToDbAsync` dřív u vadného obrázku vyhodil výjimku a všech
pět volajících mělo `catch` s hlášením „Nahrávání se nezdařilo“. Po převodu na
`IsSuccessful = false` měly tři z nich `if (result.IsSuccessful) { … }` **bez else**
a hlášení o úspěchu za smyčkou:

| Místo | Důsledek | Stav |
|---|---|---|
| `ListPhotos.razor` | „Nahráno 3 foto“ i když 2 fotky vypadly | ✅ v kole 1 |
| `Dance/Detail.razor` | totéž u fotek k tanci | ✅ v kole 2 |
| `MemberForm.razor` | člen uložen **bez** fotky, hlášeno jako úspěch | ✅ v kole 2 |

U `MemberForm` se uložení nově přeruší a dialog zůstane otevřený — fotku admin přiložil
záměrně, takže ji nelze mlčky zahodit.

Druhé poučení, k auditu: `catch`, který výjimku spolkne, musí uklidit i **stav change
trackeru**. Neuložené `AuditLog` řádky zůstávaly `Modified`, a protože kontext je scoped
na celý Blazor okruh, přehrály by se při příštím — nesouvisejícím — `SaveChanges`
uživatele. Kdyby příčinou bylo `SQLITE_BUSY` a zopakovalo se, shodilo by to uživateli
jeho vlastní zápis. Řeší se `State = EntityState.Detached` v `catch` bloku.

---

## Pokrytí

### `Demizon.Tests.Unit` (72)

| Soubor | Co hlídá |
|---|---|
| `FileUploadServiceImageTests` | Strop šířky 1200 / náhled 200 / žádný upscale, EXIF rotace do pixelů i do rozměrů, výstup vždy JPEG bez metadat, dokumenty se ukládají beze změny |
| `ContractMappingExtensionsTests` | Hranice kontraktu — všechna pole DTO, lowercase stav docházky, filtrování neviditelných videí, neprosakování `PasswordHash` do profilu |
| `AttendanceStatusContractTests` | `"yes"/"maybe"/"no"`, case-insensitivita, fallback na `No`, a hlavně že serializace a parsování jsou navzájem inverzní |
| `ResultTests` | `Ok`/`Fail` semantika, `Ok(null)` jako platný úspěch |

### `Demizon.Tests.Integration` (111)

| Soubor | Co hlídá |
|---|---|
| `RefreshTokenServiceTests` | Raw token nikdy v DB, jednorázovost (replay ochrana), expirace, revokace, rotace při novém tokenu, rozlišení tokenů se shodným prefixem, FK kaskáda |
| `AuditInterceptorTests` | `Added`/`Modified`/`Deleted`, neprosakování `PasswordHash` a `TokenHash`, audit neauditující sám sebe, oba regresní testy k chybám 2 a 3, a to že `ExecuteUpdate` audit obchází |
| `MemberServiceTests` | Soft delete přes globální filtr, historie docházky přežije smazání, `UpdateAsync` nepřepíše Google tokeny, Connect/Disconnect kalendáře |
| `AttendanceReportServiceTests` | Zkoušky (`EventId == null`) vs. akce, distinct dat vs. počet řádků, `Maybe` se nepočítá jako účast, filtr `IsAttendanceVisible`, ochrana proti dělení nulou |
| `AttendanceAndEventServiceTests` | Vložení vs. přepis podle `Id`, `LastUpdated` nastavuje služba, nesrovnalost v chování `DeleteAsync` mezi službami |
| `ModelAndMigrationsTests` | **Model odpovídá snapshotu migrací**, všechny migrace projdou od nuly, enumy jako text, unique index, kaskáda, seed data |
| `SqlitePragmaInterceptorTests` | `busy_timeout` / `journal_size_limit` / `wal_autocheckpoint` se skutečně propíšou, a to na **každé** nové spojení |

### Nejcennější jednotlivý test

`ModelAndMigrationsTests.Model_odpovida_poslednimu_snapshotu_migraci` — ostatní testy
staví schéma přes `EnsureCreated`, tedy nad aktuálním modelem, takže by chybějící
migraci samy nikdy neodhalily. Změna entity bez vygenerované migrace projde buildem
i všemi ostatními testy a rozbije se až při nasazení. Tenhle test ji zachytí hned.

---

## TODO

- [ ] **CI workflow** — `dotnet test Demizon.Backend.slnf` na každý push.
      Navázat na `build.yml` z *hosting-optimization-plan.md* (zatím nezaložený).
- [ ] **Testy controllerů** přes `WebApplicationFactory` — autorizace endpointů
      (`DatabaseController` je dnes jen `[Authorize]`, ne `Roles = "Admin"`),
      mapování status kódů, rate limiting na `/api/auth/token`.
- [ ] **bUnit na Razor komponenty** — hlavně po updatu MudBlazoru 9.3 → 9.9,
      který build projde, ale vizuální změny nezachytí.
- [ ] **`GoogleCalendarService`** — dnes netestovatelný, volá Google API přímo.
      Chtěl by rozhraní, aby šel v testu nahradit dvojníkem.
- [ ] **Testy purge jobu** na `AuditLog` / `RefreshTokens` / `SentNotifications`,
      až vznikne (Priorita 2 v *hosting-optimization-plan.md*).
- [ ] **Zátěžový test paměti** obrazového pipeline — dnes je ověřený jen ručně
      (24 Mpx → 69 MB RSS, 100 Mpx → 104 MB RSS). Automatizovat proti stropu 128 MB.
      Souvisí: strop alokátoru se nastavuje globálně v `AddCoreServices`, kterou unit
      testy nevolají, takže `AllocationLimitMegabytes = 128` zatím není pokrytý vůbec.
- [ ] **Odlehčit referenci `Demizon.Tests.Unit → Demizon.Mvc`.** Je tam kvůli
      `ContractMappingExtensions` a `ParseStatus`, ale táhne s sebou celý web host
      včetně `appsettings.*.json` a `demizon.sqlite` do test outputu (`bin/` je
      v gitignore, takže nic neuniká — jen je to zbytečná zátěž). Vyřeší se buď
      přesunem těchto testů do budoucího `Demizon.Tests.Web`, nebo tím, že
      `demizon.sqlite` přestane být commitnutý a kopírovaný (Priorita 3
      v *hosting-optimization-plan.md*).

## Konvence

- **Názvy testů česky, se podtržítky** — `UploadImageToDbAsync_nezvetsuje_obrazky_mensi_nez_strop`.
  Popisují chování, ne implementaci; jméno má být čitelné ve výpisu selhání.
- **Žádné binární fixtures v repu.** Obrázky generuje `TestImages` za běhu.
- **Žádná assertion knihovna nad rámec xUnitu.** FluentAssertions v8 přešly na
  komerční licenci; holý `Assert` je bez závislosti a bez licenčního rizika.
- **Nový kontext na operaci** (`DatabaseFixture.NewContext()`) — test si tím vynutí
  čtení z databáze, ne z change trackeru předchozího zápisu.
- **Když test najde chybu**, oprava jde do produkčního kódu a v testu zůstane
  komentář, co konkrétně regredovalo. Testy tady slouží i jako dokumentace pastí.
