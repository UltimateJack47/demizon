# Testovací strategie

> **Živý dokument.** Průběžně aktualizovat při každé dokončené položce.
> Založeno: 2026-09-02. Poslední aktualizace: 2026-09-09 (Flutter testy 26).
>
> Rozcestník dokumentace: [`README.md`](README.md). Dopředný plán:
> [`STATUS.md`](STATUS.md).
>
> Co dělat dál po auth testech a diskové vlně: [`STATUS.md`](STATUS.md).

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
| `Demizon.Tests.Unit` | Čistá logika bez I/O — mapování na DTO, kontrakt docházky, obrazový pipeline, `Result`, JWT; a přes `WebApplicationFactory` i HTTP auth, bootstrap, limity, lokalizace a kompenzace kalendáře | ~10 s / 126 testů |
| `Demizon.Tests.Integration` | Chování nad **skutečnou SQLite** — služby, interceptory, EF model, migrace, soft delete napříč relacemi, kontrakt „služba nevyhodí výjimku“ | ~3 s / 169 testů |
| `Demizon.Tests.E2E` | Skutečný prohlížeč nad **běžící aplikací** — cookie přihlášení, Blazor okruh, layout MudBlazoru, validace dialogových formulářů | ~85 s / 53 testů |
| `demizon_flutter/test` | Kontrakt mobilního klienta s API (statusy, data, deep-link z notifikace). Není v `Demizon.Backend.slnf` — spouští se `flutter test` v `demizon_flutter/` | ~1 s / 26 testů |

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
dotnet test Demizon.Backend.slnf          # unit + integrační (rychlá brána)
dotnet test Demizon.Tests.Unit            # jen rychlá logika
dotnet test Demizon.E2E.slnf              # E2E v prohlížeči (chce Chromium)
```

**E2E má vlastní filtr záměrně.** `Demizon.Backend.slnf` tak zůstává bez
závislosti na prohlížeči — kdo chce jen vědět, že logika drží, nemusí stahovat
150 MB Chromia. V CI jsou to dvě úlohy; ta E2E si Chromium doinstaluje
(`playwright.ps1 install --with-deps chromium`) a nahraje screenshoty jako
artefakt běhu.

Lokálně jednorázově:

```bash
pwsh Demizon.Tests.E2E/bin/Release/net10.0/playwright.ps1 install chromium
```

> ⚠️ **`dotnet test Demizon.slnx` neprojde.** `Demizon.Maui` vyžaduje workload
> `maui-android`, který na běžném stroji ani v CI není. Proto je v repu
> `Demizon.Backend.slnf` — solution filter se všemi projekty kromě MAUI.
> (MAUI se navíc přepisuje do Flutteru na samostatné branchi.)

---

## Nalezené chyby

Chyby, které testy odhalily a které nešly vidět čtením kódu ani buildem.

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
> a ten má podle *disk-optimalizace* z auditu úplně vypadnout
> (whitelist entit, Priorita 2). Po té změně bude extra zápis vzácný.

### ✅ 3. Audit log měl pro tutéž entitu dva různé názvy typu

`entry.Entity.GetType().Name` vrací s `UseLazyLoadingProxies()` název **proxy typu**.
Entita načtená z DB se proto auditovala jako `"MemberProxy"`, ale nově vložená jako
`"Member"` — v audit tabulce se nedalo filtrovat podle typu.

**Oprava:** `entry.Metadata.ClrType.Name` (typ z EF modelu, ne runtime typ instance).

### ✅ 4. `TokenResponse.MemberId` se nikdy neplnilo

Kontrakt má `MemberId`, Flutter i MAUI ho ukládají (`if (response.MemberId != 0)`),
ale `AuthController.Token`/`Refresh` ho do odpovědi nepředávaly — vždy tedy 0.
Mobilní klient po přihlášení neměl member id a admin-za-člena endpointy v Flutteru
by šly s prázdným id.

**Oprava:** do `TokenResponse` se předává `member.Id`. Hlídá `AuthApiTests`.

### ✅ 5. Admin API endpointy vyžadovaly cookie místo JWT

`EventsController` a `VideosController` mají na třídě `[Authorize(AuthenticationSchemes = JwtBearer)]`,
ale mutující akce měly jen `[Authorize(Roles = "Admin")]`. Druhý atribut bere výchozí
schéma (cookie). JWT admin z Flutteru by na DELETE/PUT dostal 401. Dva notify endpointy
už JwtBearer měly — tyhle ne.

**Oprava:** všechny admin akce na těch controllerech mají explicitně JwtBearer + Admin.
Hlídá `AuthApiTests.Admin_endpoint_*`.

### ✅ 6. Seed endpoint hashoval jiné heslo, než vracel

`DatabaseController.SeedDatabase` hashoval `"testpass"`, ale v JSON odpovědi posílal
`password = "admin123"`. První přihlášení po seedu by tedy nikdy neprošlo.

**Oprava:** hashuje se `admin123`, stejně jako v odpovědi.

> **Nahrazeno 2026-09-09.** Endpoint už žádné zadrátované heslo nemá — login
> i heslo přicházejí v requestu, v odpovědi se heslo nevrací a celý endpoint je
> zamčený za `Bootstrap:SeedToken`. Tím ta chyba přestala existovat i jako
> možnost. Viz `SeedEndpointTests` a *disk-optimalizace*, sekce
> „Jak vznikne první admin“.


### ✅ 4. `[property: Required]` na záznamu shodí endpoint v .NET 10

Nový `SeedAdminRequest` je `record` s validačními atributy. Napsané byly jako
`[property: Required]`, což je zvyk z verzí, kde DataAnnotations atributy na
pozičních parametrech ignorovaly. .NET 10 přidal validaci záznamů a trvá na
opačném zápisu — atribut musí sedět **na parametru** primárního konstruktoru:

```
System.InvalidOperationException: Record type 'SeedAdminRequest' has validation
metadata defined on property 'Email' that will be ignored. 'Email' is a parameter
in the record primary constructor and validation metadata must be associated with
the constructor parameter.
```

Výjimka vzniká při stavbě metadat akce, tedy **před** spuštěním kódu, takže
každý request na endpoint skončil 500 — včetně těch, které měly vrátit 400 nebo
404. Deset nových testů padlo naráz se stejným výsledkem, což bylo přesně to
vodítko: kdyby šlo o chybu v logice, každý test by padl jinak.

> **Poučení k diagnostice:** v `Development` **není** zaregistrovaný žádný
> exception handler ani `UseDeveloperExceptionPage`, takže `WebApplicationFactory`
> vrátí 500 s prázdným tělem a nic se nezaloguje. Kdo hledá příčinu, ať appku
> spustí naostro (`dotnet run`) a přečte konzoli. Pomůcka
> `SeedEndpointTests.AssertStatusAsync` proto při neshodě přiloží tělo odpovědi —
> „expected 401, actual 500“ bez těla je jen hádání.
>
> A pozor: `dotnet run` bere URL z `launchSettings.json`, takže
> `ASPNETCORE_URLS` se ignoruje.

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
**spolkne každou výjimku, zaloguje ji a vrátí neúspěch**. Tehdy to bylo
`bool`; dnes je to `Result` (viz níž) — vzor „služba spolkne, volající se
musí zeptat“ ale zůstává:

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
  <br>Kolo 7 ale ukázalo, že tím je pokrytá jen **cesta selhání**: osiřelá událost
  vzniká i při **úspěšném** uložení nové docházky, protože `model.Id` zůstane 0.
  To je samostatná chyba se změnou kontraktu služby, viz TODO níž.

---

## Pokrytí

### `Demizon.Tests.Unit` (96)

| Soubor | Co hlídá |
|---|---|
| `FileUploadServiceImageTests` | Strop šířky 1200 / náhled 200 / žádný upscale, EXIF rotace do pixelů i do rozměrů, výstup vždy JPEG bez metadat, dokumenty se ukládají beze změny |
| `FileUploadServiceQuotaTests` | `MaxFileBytes` odmítne dokument i obrázek před dekódováním; odmítnutí z `IStorageQuotaService` se propíše do `ErrorMessage` |
| `ContractMappingExtensionsTests` | Hranice kontraktu — všechna pole DTO, lowercase stav docházky, filtrování neviditelných videí, neprosakování `PasswordHash` do profilu |
| `AttendanceStatusContractTests` | `"yes"/"maybe"/"no"`, case-insensitivita, fallback na `No`, a hlavně že serializace a parsování jsou navzájem inverzní |
| `ResultTests` | `Ok`/`Fail` semantika, `Ok(null)` jako platný úspěch |
| `TokenServiceTests` | JWT nese login, roli a `PrimarySid`; validace odmítne cizí klíč, issuer i expirovaný token |
| `ClaimsPrincipalExtensionsTests` | `GetMemberId` čte `PrimarySid` a bez claimu hodí |
| `AuthApiTests` | HTTP login/refresh, soft-delete a externista, `TokenResponse.MemberId`, 401 bez JWT, 403/404 na admin endpointu, profil bez `passwordHash` |
| `HttpLocalizationTests` | Vyjednávání jazyka podle `Accept-Language`: `cs-CZ` i bare `cs` dostanou češtinu, `sk-SK` se remapuje, ostatní angličtinu. Chytilo, že bare `cs` (posílá ho Firefox) padalo na výchozí angličtinu |
| `GoogleCalendarCompensationTests` | Kompenzace mezi kalendářem a databází přes dvojníka: vytvořená událost se po selhaném uložení ruší, ID smazané se z databáze nuluje, a příští „přijdu“ pak událost znovu vytvoří |
| `ApiLimitTests` | HTTP 429 na `/api/auth/token` a že vyčerpané okno neblokuje endpointy bez politiky `auth`; kontrakt uploadu při plné kvótě |
| `CoreRegistrationTests` | `AddCoreServices` nastaví globální strop alokátoru ImageSharpu a zaregistruje služby, které host používá |
| `SeedEndpointTests` | Bootstrap prvního admina: 404 bez `Bootstrap:SeedToken`, 401 na špatný i zkrácený `X-Seed-Token`, 400 na krátké heslo, 409 nad neprázdnou databází **i nad soft-smazaným členem**, heslo se nevrací v odpovědi |

### `Demizon.Tests.Integration` (155)

| Soubor | Co hlídá |
|---|---|
| `ServiceExceptionContractTests` | Kontrakt „`CreateAsync`/`DeleteAsync` nikdy nevyhodí výjimku, vždy vrátí `Result`“ — spouští služby nad zavřeným kontextem. Vzniklo z regrese: vyhledání entity vytažené mimo `try` |
| `SoftDeleteRelationTests` | `Include(a => a.Member)` zahazuje docházku soft-smazaného člena (EF varování 10622), stejný dotaz bez `Include` ji vrátí, `IgnoreQueryFilters` ji vrátí i s `Include`; refresh token smazaného člena se zastaví až o krok dál |
| `RefreshTokenServiceTests` | Raw token nikdy v DB, jednorázovost (replay ochrana), expirace, revokace, rotace při novém tokenu, rozlišení tokenů se shodným prefixem, FK kaskáda |
| `AuditInterceptorTests` | `Added`/`Modified`/`Deleted`, neprosakování `PasswordHash`, whitelist (`RefreshToken`/`File`/`DeviceToken`/`SentNotification`), audit neauditující sám sebe, regresní testy k chybám 2 a 3, **selhání dopsání klíčů** (přes `FailAuditFixupInterceptor`) a to že `ExecuteUpdate` audit obchází |
| `MemberServiceTests` | Soft delete přes globální filtr, historie docházky přežije smazání, `UpdateAsync` nepřepíše Google tokeny, Connect/Disconnect kalendáře |
| `AttendanceReportServiceTests` | Zkoušky (`EventId == null`) vs. akce, distinct dat vs. počet řádků, `Maybe` se nepočítá jako účast, filtr `IsAttendanceVisible`, ochrana proti dělení nulou |
| `AttendanceAndEventServiceTests` | Vložení vs. přepis podle `Id`, `LastUpdated` nastavuje služba, nesrovnalost v chování `DeleteAsync` mezi službami |
| `ChangeTrackerRecoveryTests` | Služby po neúspěšném zápisu uklidí change tracker, takže vrácené `false` skutečně znamená „neuložilo se“ a další pokus projde |
| `ModelAndMigrationsTests` | **Model odpovídá snapshotu migrací**, všechny migrace projdou od nuly, enumy jako text, unique index, kaskáda, seed data |
| `SqlitePragmaInterceptorTests` | `busy_timeout` / `journal_size_limit` / `wal_autocheckpoint` / `auto_vacuum=INCREMENTAL` se skutečně propíšou, a to na **každé** nové spojení |
| `StorageQuotaServiceTests` | per-file / count / total-bytes kvóty; `FileService.CreateAsync` při odmítnutí nic neuloží |
| `DiskMaintenanceServiceTests` | purge AuditLog 90 dní, revokované i expirované refresh tokeny, SentNotifications 180 dní; netýká se členů ani souborů |
| `FileBlobLoadingTests` | `GetOneAsync` / seznamy nenačtou BLOBy; `UpdateAsync` nemaže Data; `GetContentAsync` vrací jen požadovaný sloupec |

### `Demizon.Tests.E2E` (48)

Host se spouští jako **samostatný proces** na reálném Kestrelu, ne přes
`WebApplicationFactory` — `TestServer` nemá otevřený socket, takže se na něj
prohlížeč nemá jak připojit.

| Soubor | Co hlídá |
|---|---|
| `AuthFlowTests` | Cookie přihlášení, které `AuthApiTests` (JWT) nepokrývají: form post na `/ProcessLogin`, redirect, `HttpOnly` cookie, nepřihlášený admin obsah nevidí, odhlášení přístup zavře |
| `PublicPageTests` | 6 veřejných rout × (vykreslení bez chyby v konzoli + žádný vodorovný přesah); naběhnutí Blazor okruhu |
| `AdminWalkthroughTests` | 9 admin stránek × (desktop + mobil 390 px), popisky buněk tabulek na mobilu, přítomnost MudBlazor providerů, otevření a zavření dialogu, **validace všech čtyř dialogových formulářů** |
| `MudBlazorLayoutTests` | Nulové rozměry interaktivních prvků, přetékání na 390 px |

**Proč chyby v konzoli jako assertion:** v `Development` není zaregistrovaný
žádný exception handler, takže selhání renderu v Blazoru se nikde jinde
neprojeví — server vrátí 500 s prázdným tělem, nebo stránka jen přestane
reagovat.

**Proč ne pixelové porovnávání:** fonty se renderují jinak na jiném stroji
i po aktualizaci prohlížeče, takže by baseline padala z důvodů, které
s aplikací nesouvisejí. Screenshoty se ukládají do `e2e-artifacts/` jako
artefakt k prohlédnutí. Vyplatilo se: dva ze tří vizuálních nálezů této vlny
našel až pohled na obrázek, ne assertion (viz `features/kvalita-result-testy/review.md`).

---

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
      `DancesController.cs:129,143`, `FilesController.cs:126`,
      a v `MemberAttendance.razor.cs` ta dvě místa, která ukládají a mažou
      `GoogleEventId` (opravená jsou jen ta dvě, která rozhodovala o vytvoření
      události v kalendáři).
      Lepší než doplňovat kontroly jednu po druhé je převést služby na
      `Result`/`Result<T>` z `Demizon.Common` — ten typ v repu už je a přesně na tohle
      se hodí, a compiler pak zahození výsledku umí nahlásit. Chce testy na oba směry.
- [ ] **Osiřelé události v Google Calendaru u nově vytvořené docházky.**
      Nezávisí na tom, jestli uložení selže — děje se to i na **úspěšné** cestě.
      `MemberAttendance.razor.cs` má `model.Id = attendanceResult.Id;`, což je
      no-op: `AttendanceForm` binduje přímo na předaný objekt a vrací tutéž
      instanci, takže `attendanceResult` **je** `model`. A `ToEntity()` vyrábí novou
      entitu, na kterou klíč přiřadí databáze — do view modelu se nikdy nedostane.
      U nové docházky tedy `model.Id` zůstalo 0, podmínka
      `if (createdId is not null && model.Id != 0)` v `SyncGoogleCalendarAsync`
      neprošla a ID vytvořené události se nikam nezapsalo. Pozdější přepnutí na
      „nepřijdu“ ji pak nemělo čím smazat.
      > **Opraveno 2026-09-09** a bez změny kontraktu služby, kterou tenhle
      > odstavec předpokládal. Na vložené cestě je předaná entita ta, kterou EF
      > trackuje, takže jí po `SaveChanges` dopíše vygenerovaný klíč — stačilo ji
      > podržet v proměnné (`var entity = attendanceResult.ToEntity()`) a klíč
      > z ní přenést do `model.Id`. Hlídá
      > `CreateOrUpdateAsync_vyplni_Id_na_predane_entite` (a sesterský test, že
      > se klíč po neúspěchu **nenastaví**, protože volající se rozhoduje podle
      > `Id != 0`), celý cyklus pak
      > `Nova_dochazka_umi_prijmout_a_pozdeji_zahodit_GoogleEventId`.
      > Ve stejném průchodu se přestaly zahazovat dvě návratové hodnoty: když
      > se ID události nepodaří k docházce zapsat, událost se z kalendáře hned
      > zruší (nedohledatelná událost = nesmazatelná událost), a když se po
      > smazání nepodaří vyprázdnit `GoogleEventId`, uživatel o tom dostane
      > varování — jinak by příští „přijdu“ ochrana proti duplikátům umlčela.
- [x] **CI workflow** — `.github/workflows/test.yml` spouští
      `dotnet test Demizon.Backend.slnf` na push/PR. Docker build/deploy dál čeká
      na rozhodnutí o registry (viz *nasazeni.md*).
- [x] **Testy auth controllerů** přes `WebApplicationFactory` (`AuthApiTests`)
      a bootstrap endpointu (`SeedEndpointTests`).
      Zbývá rate limiting na `/api/auth/token` (v test hostu je limit zvednutý,
      aby se suite nevešla do 5 req/min) a zbylé admin endpointy mimo events.
      > `WebHostCollection` teď serializuje **všechny** třídy nad
      > `WebApplicationFactory` a obě fixture si staví host už v konstruktoru.
      > Hosty se konfigurují proměnnými prostředí **procesu** (`Program.cs` čte
      > connection string dřív než `ConfigureWebHost`), takže líně postavený host
      > by se trefil do databáze té fixture, která env nastavila jako poslední.
      > Seed token se proto nastavuje přes `PostConfigure`, ne přes env — je to
      > per-host, ne per-proces.
- [ ] **bUnit na Razor komponenty.** Priorita klesla: `Demizon.Tests.E2E` teď
      pokrývá layout, dialogy i interakci nad skutečným prohlížečem, tedy to,
      proč byl bUnit na seznamu. Zbývá pro něj případ, kdy je potřeba testovat
      jednu komponentu izolovaně s vnucenými parametry — na to je E2E hrubé.
- [ ] **`GoogleCalendarService`** — dnes netestovatelný, volá Google API přímo.
      Chtěl by rozhraní, aby šel v testu nahradit dvojníkem.
- [x] **Testy purge jobu** na `AuditLog` / `RefreshTokens` / `SentNotifications`
      (`DiskMaintenanceServiceTests`). Kvóty: `StorageQuotaServiceTests` +
      `FileUploadServiceQuotaTests`.
- [ ] **Zátěžový test paměti** obrazového pipeline — dnes je ověřený jen ručně
      (24 Mpx → 69 MB RSS, 100 Mpx → 104 MB RSS). Automatizovat proti stropu 128 MB.
      > To, že `AddCoreServices` strop **vůbec nastaví**, už pokryté je
      > (`CoreRegistrationTests`). Chybí jen měření skutečného RSS, které chce
      > vlastní proces — v testovacím hostu se neizoluje.
- [ ] **Oddělit HTTP testy od čisté logiky.** `Demizon.Tests.Unit` teď referencuje
      `Demizon.Mvc` kvůli `ParseStatus`, mapování *a* `WebApplicationFactory`.
      Táhne to web host do outputu. Až bude `Demizon.Tests.Web`, `AuthApiTests`
      sem patří; zbytek unitů by Mvc tahat nemusel.

## Kontrakt `Result` a co z něj plyne pro testy

Zápisové operace v `Demizon.Core` vracejí `Result` / `Result<int>`, ne `bool`.
Pro testy to znamená tři věci:

- **`ResultAssert.Ok` / `.Failed` místo `Assert.True/False`.** Holý
  `Assert.True(r.IsSuccess)` při selhání vypíše „Expected: True“ a zahodí
  `Error` — tedy jedinou informaci, která říká proč.
- **U neúspěchu se kontroluje i `ErrorKind`**, když na něm něco stojí:
  `NotFound` mapuje controller na 404, `Rejected` na 4xx s textem pro
  uživatele. `ResultAssert.Failed(result, ResultErrorKind.NotFound)`.
- **Kompilátor zahození výsledku nenahlásí** (C# nemá `[[nodiscard]]`, CA1806
  se vztahuje jen na `[Pure]` metody). Že volající výsledek kontrolují, musí
  hlídat testy a review — proto ty testy míří na **volající**, ne jen na služby.

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
