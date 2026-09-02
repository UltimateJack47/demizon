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
| `Demizon.Tests.Integration` | Chování nad **skutečnou SQLite** — služby, interceptory, EF model, migrace | ~3 s / 133 testů |

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

### Kolo 3: test, který opravu netestoval

Kolo 3 přineslo nález, který stojí za zapamatování: **dva regresní testy k opravě
z kola 2 tu opravu vůbec neprocházely.** Oba šly happy path, kde vnořené uložení
uspěje, takže `catch` blok s `EntityState.Detached` se nikdy nespustil. Reviewer to
prokázal tím, že opravu odstranil a testy zůstaly zelené.

Řeší to `FailAuditFixupInterceptor` — testovací interceptor registrovaný **za** ten
auditní, který shodí právě a jen to vnořené uložení. Rozlišuje ho podle stavu audit
řádků: při původním uložení jsou `Added`, při dopisování klíčů `Modified`.

Ověřeno oběma směry: bez opravy padnou právě dva testy, s opravou projde 27/27.

> **Poučení:** u opravy, která žije v `catch` bloku, nestačí napsat test, který
> po opravě projde. Je nutné ho spustit i **proti kódu bez opravy** a vidět ho
> zčervenat — jinak není jasné, jestli testuje opravu, nebo jen happy path.

Vedlejší poznatek z ladění: dva testy nejdřív padaly ze **zastaralého buildu**
`Demizon.Dal.dll` v test outputu. Signatura selhání je přitom identická se skutečnou
chybou, takže než začneš hledat příčinu v kódu, vyplatí se smazat `obj/` a `bin/`.

### Kolo 4: premisa opravy stála na počítadle, které nic negarantovalo

Kolo 4 našlo, že celá logika „počítej skutečně uložené, ne předané“ stála na
`uploaded++` **za** voláním `FileService.CreateAsync(entity)`, jehož návratovou
hodnotu nikdo nekontroloval. A ta služba — jako 11 dalších metod v 6 službách —
**spolkne každou výjimku, zaloguje ji a vrátí `false`**:

```csharp
public async Task<bool> CreateAsync(File file)
{
    try { await DemizonContext.AddAsync(file); await DemizonContext.SaveChangesAsync(); return true; }
    catch (Exception ex) { logger.LogError(ex, "Failed to process File operation."); return false; }
}
```

Důsledek: při plném disku nebo porušené constraintě vrátí každý insert `false`,
`uploaded` je 3, mřížka se zbytečně obnoví a admin dostane zelené „Nahráno 3 foto“,
přesto že se neuložilo nic. Vnější `catch`, na kterém byla oprava postavená, je pro
selhání zápisu do DB **nedosažitelný**.

> **Poučení:** služba, která vrací `bool` místo výjimky, přenáší odpovědnost na
> volajícího — a `await Sluzba.CreateAsync(x);` bez kontroly návratové hodnoty je
> tichá ztráta dat, kterou kompilátor nenahlásí.

Opraveno dvěma vrstvami. **Ve službách** samotných: každý `catch` teď volá
`DiscardPendingChange`, protože EF po výjimce ze `SaveChangesAsync` change tracker
nevrací a entita by se v Blazor okruhu vložila při příštím — nesouvisejícím — uložení.
Bez toho vrácené `false` neznamenalo „neuložilo se“, ale „neuložilo se *teď*“.
**Ve stránkách**, které tento PR mění: kontrola návratové hodnoty místo zeleného
hlášení. Vzor zahazovaných výsledků ale zůstává na dobré dvacítce dalších míst —
viz TODO níž.

### Vlastní revize: entitně cílený úklid propouštěl tři případy

Kolo 6 review spadlo na session limitu ještě před čtením diffu, tak jsem si změnu
z kola 5 prošel sám — a našel v ní tři díry. Původní `DiscardPendingChange(entity)`
cílil na jednu konkrétní instanci, což nestačí:

| Případ | Proč cílení nestačilo |
|---|---|
| **Grafy** | `AddAsync(member)` u člena s fotkou nastraží jako `Added` i tu fotku. Odpojení člena ji nechalo v trackeru — a přesně tenhle graf ukládá `MemberForm.razor` při zakládání člena s profilovkou. |
| **Update přes jinou instanci** | `AttendanceService.CreateOrUpdateAsync` kopíruje hodnoty do *načtené* entity, takže trackovaná je ona, ne ta předaná. `Entry()` na předané byl **no-op** a oprava na té cestě nedělala nic. |
| **Audit** | `AuditSaveChangesInterceptor` přidává `AuditLog` řádky v `SavingChangesAsync`. Po selhání zůstaly `Added` a vložily by se s příštím uložením jako záznam o změně, která se nikdy nestala. |

Pomůcka je proto bezparametrová `DiscardPendingChanges()` a maže **celý** tracker.
Volající služby ukládají vždy hned po své vlastní změně, takže všechno rozpracované
v momentě selhání *je* ta selhaná operace.

Navíc se u `Modified` zahazují i **hodnoty v paměti** (`CurrentValues.SetValues(OriginalValues)`),
ne jen stav. Bez toho by entita zůstala s nezapsanými hodnotami označená jako čistá
a další čtení z téhož kontextu by vydalo třeba člena jako smazaného, přesto že soft
delete selhal.

Ověřeno oběma směry: s entitně cílenou variantou padá právě těch 5 testů, které ty
tři díry pokrývají.

> **Poučení:** u opravy change trackeru je „která entita“ špatná otázka. EF trackuje
> grafy, interceptory přidávají vlastní entity a `SetValues` píše do jiné instance,
> než která přišla na vstup. Rozsah selhání je celý tracker, ne jeden objekt.

### Kolo 6: úklid chyběl i tam, kde se chyba hlásí výjimkou

Předchozí kolo opravilo šest metod vracejících `bool`. Osm dalších ale chybu hlásí
**výjimkou** (`UpdateAsync` v pěti službách, `SetCancelledAsync`,
`Connect`/`DisconnectGoogleCalendarAsync`) a tracker po sobě neuklízely.

`docs/testing-plan.md` ty metody z opravy vyloučil s tím, že „do vzoru nepatří —
vrací `Task` a výjimku propouští“. To bylo jen z poloviny pravda: **zahazovaná
návratová hodnota a únik v trackeru jsou dvě různé chyby** a neuplatňuje se jen ta
první. Naměřeno: po neúspěšné úpravě člena vrátilo následující — úplně nesouvisející —
`EventService.CreateAsync` **false** a admin o zakládanou akci přišel.

Řeší to `SaveChangesWithRecoveryAsync()`: uloží a při selhání vyčistí tracker, než
výjimku pustí dál. Jeden call na místo, kde dřív bylo `SaveChangesAsync()`.

> **Poznámka k vyhodnocení dopadu.** Review popsalo změnu z předchozího kola tak, že
> „mění tiché přehrání na jednu pozdější selhanou operaci“, tedy jako zhoršení.
> Naměřeno to tak není: **před** ní zůstal vadný zápis v trackeru navždy a selhávalo
> každé další uložení v okruhu; **po** ní se tracker po první selhané operaci vyčistí
> a další už projde. Bylo to tedy zlepšení, jen nedokončené. Kolo 6 odstranilo
> i tu jednu selhanou operaci.

Zbylé tři nálezy kola 6:

- Dokumentová smyčka v `Dance/Detail.razor` nebyla přestavěná jako fotková: `uploaded`,
  hlášení i obnova zůstaly v `try`, takže výjimka u pozdějšího souboru zahodila
  informaci o těch dřívějších, které se uložit stihly.
- Tamtéž chybělo `else` u `result.IsSuccessful` — odmítnutý dokument neskončil ani
  ve `failures`, ani v `uploaded`, takže klik vypadal, že neudělal vůbec nic.
- **`MemberAttendance.razor.cs`** zahazoval výsledek a pokračoval k synchronizaci
  s Google Calendarem. Vznikla tam reálná událost pro docházku, která se neuložila —
  a protože `model.Id` zůstal 0, vrácené ID se nikam nezapsalo, takže tu osiřelou
  událost už nešlo smazat ani pozdějším přepnutím na „nepřijdu“. Jediné místo ze
  seznamu níž, které jsem opravil i mimo rozsah PR, právě kvůli tomu externímu
  a nevratnému efektu.

---

## Pokrytí

### `Demizon.Tests.Unit` (72)

| Soubor | Co hlídá |
|---|---|
| `FileUploadServiceImageTests` | Strop šířky 1200 / náhled 200 / žádný upscale, EXIF rotace do pixelů i do rozměrů, výstup vždy JPEG bez metadat, dokumenty se ukládají beze změny |
| `ContractMappingExtensionsTests` | Hranice kontraktu — všechna pole DTO, lowercase stav docházky, filtrování neviditelných videí, neprosakování `PasswordHash` do profilu |
| `AttendanceStatusContractTests` | `"yes"/"maybe"/"no"`, case-insensitivita, fallback na `No`, a hlavně že serializace a parsování jsou navzájem inverzní |
| `ResultTests` | `Ok`/`Fail` semantika, `Ok(null)` jako platný úspěch |

### `Demizon.Tests.Integration` (133)

| Soubor | Co hlídá |
|---|---|
| `RefreshTokenServiceTests` | Raw token nikdy v DB, jednorázovost (replay ochrana), expirace, revokace, rotace při novém tokenu, rozlišení tokenů se shodným prefixem, FK kaskáda |
| `AuditInterceptorTests` | `Added`/`Modified`/`Deleted`, neprosakování `PasswordHash` a `TokenHash`, audit neauditující sám sebe, regresní testy k chybám 2 a 3, **selhání dopsání klíčů** (přes `FailAuditFixupInterceptor`) a to že `ExecuteUpdate` audit obchází |
| `MemberServiceTests` | Soft delete přes globální filtr, historie docházky přežije smazání, `UpdateAsync` nepřepíše Google tokeny, Connect/Disconnect kalendáře |
| `AttendanceReportServiceTests` | Zkoušky (`EventId == null`) vs. akce, distinct dat vs. počet řádků, `Maybe` se nepočítá jako účast, filtr `IsAttendanceVisible`, ochrana proti dělení nulou |
| `AttendanceAndEventServiceTests` | Vložení vs. přepis podle `Id`, `LastUpdated` nastavuje služba, nesrovnalost v chování `DeleteAsync` mezi službami |
| `ChangeTrackerRecoveryTests` | Služby po neúspěšném zápisu uklidí change tracker, takže vrácené `false` skutečně znamená „neuložilo se“ a další pokus projde |
| `ModelAndMigrationsTests` | **Model odpovídá snapshotu migrací**, všechny migrace projdou od nuly, enumy jako text, unique index, kaskáda, seed data |
| `SqlitePragmaInterceptorTests` | `busy_timeout` / `journal_size_limit` / `wal_autocheckpoint` se skutečně propíšou, a to na **každé** nové spojení |

### Nejcennější jednotlivý test

`ModelAndMigrationsTests.Model_odpovida_poslednimu_snapshotu_migraci` — ostatní testy
staví schéma přes `EnsureCreated`, tedy nad aktuálním modelem, takže by chybějící
migraci samy nikdy neodhalily. Změna entity bez vygenerované migrace projde buildem
i všemi ostatními testy a rozbije se až při nasazení. Tenhle test ji zachytí hned.

---

## TODO

- [ ] **Projít zbylá zahazovaná `bool` z Core služeb.** Vzor má **12 metod**:
      `CreateAsync` + `DeleteAsync` v `Dance`, `Event`, `File`, `Member`
      a `VideoLink`, plus `CreateOrUpdateAsync` + `DeleteAsync` v `Attendance`.
      (`UpdateAsync` a spol. do vzoru **zahazované návratové hodnoty** nepatří —
      hlásí výjimkou; únik v trackeru ale měly a je opravený přes
      `SaveChangesWithRecoveryAsync`.)
      Samotné služby už po sobě uklidí change tracker, ale **volající návratovou
      hodnotu většinou ignorují** a hlásí úspěch. Tento PR opravil jen místa, která
      už mění; zbývají minimálně: `ListEvents.razor:122,145`,
      `ListVideoLinks.razor:72,107`, `ListMembers.razor:192`, `ListDances.razor:108`,
      `AttendancesController.cs` (6×),
      `DancesController.cs:129,143`, `FilesController.cs:126`.
      Lepší než doplňovat kontroly jednu po druhé je převést služby na
      `Result`/`Result<T>` z `Demizon.Common` — ten typ v repu už je a přesně na tohle
      se hodí, a compiler pak zahození výsledku umí nahlásit. Chce testy na oba směry.
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
