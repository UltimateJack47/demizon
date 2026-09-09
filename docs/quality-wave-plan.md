# Vlna kvality: Result refactor, automatizované testy, vizuální QA

> **Živý dokument.** Průběžně aktualizovat při každé dokončené položce.
> Založeno: 2026-09-09. Poslední aktualizace: 2026-09-09.
>
> Účel: plán a **průběžný stav** této vlny, aby ji šlo dokončit i po přerušení
> session. Každý krok má stav, takže navazující session pozná, kde se zastavit.

Předchůdci: [`next-wave-plan.md`](next-wave-plan.md) (co bylo hotové před touto
vlnou), [`testing-plan.md`](testing-plan.md) (konvence testů, nalezené chyby),
[`hosting-optimization-plan.md`](hosting-optimization-plan.md) (nasazení).

**Výchozí stav:** `master` na `2770d60`, **268 testů zelených**
(106 unit + 162 integration), CI přes `.github/workflows/test.yml`.

---

## Zadání

1. **Result refactor** — 12 metod v 6 službách vrací `bool`, volající návratovou
   hodnotu většinou ignorují a hlásí úspěch.
2. **Maximum automatizovaného testování** — unit, integrační, **E2E** (dnes žádné).
3. **Vizuální QA MudBlazoru 9.9** — co nejvíc automatizovaně, přes Claude in Chrome.
4. **Code review** nad výsledkem.

Odloženo uživatelem: Firebase, notifikační stack, fyzický telefon, nasazení.

---

## Pořadí a proč

Refactor mění signatury služeb, takže se dotkne **64 míst v testech**. Musí jít
první, jinak by se testy psané před ním přepisovaly dvakrát. E2E naopak jede přes
HTTP a prohlížeč, takže je na signaturách nezávislé — může jít kdykoli po něm.

| # | Krok | Stav |
|---|---|---|
| 1 | Plán (tento dokument) | ✅ hotovo |
| 2 | Result refactor: služby + volající + testy | ✅ hotovo |
| 3 | E2E infrastruktura (Playwright) | ✅ hotovo |
| 4 | E2E scénáře | ✅ hotovo |
| 5 | Doplnit unit/integrační díry | ✅ hotovo |
| 6 | Vizuální QA MudBlazor 9.9 | ✅ hotovo včetně ručního proklikání |
| 7 | Code review celé vlny | ✅ hotovo |

---

## 1. Result refactor

### Rozsah

12 metod, 6 služeb. `GoogleCalendarService.UpdateEventAsync` / `DeleteEventAsync`
do vzoru **nepatří** — vrací `bool` o výsledku cizího API, ne o vlastním zápisu.

| Služba | Metody |
|---|---|
| `AttendanceService` | `CreateOrUpdateAsync`, `DeleteAsync` |
| `DanceService` | `CreateAsync`, `DeleteAsync` |
| `EventService` | `CreateAsync`, `DeleteAsync` |
| `FileService` | `CreateAsync`, `DeleteAsync` |
| `MemberService` | `CreateAsync`, `DeleteAsync` |
| `VideoLinkService` | `CreateAsync`, `DeleteAsync` |

Volající: 35 míst v 15 souborech (6 controllerů, 8 Razor stránek,
`AuthenticationService`). V testech 64 použití.

### Cílový tvar

```csharp
Task<Result<int>> CreateAsync(T entity);            // Value = nový klíč
Task<Result<int>> CreateOrUpdateAsync(Attendance a); // Value = klíč uloženého řádku
Task<Result>      DeleteAsync(int id);
```

`Result<int>` u `CreateAsync` je zdarma — EF klíč po `SaveChanges` má — a rovnou
řeší to, co u docházky muselo obcházet držení entity v proměnné.

### Co refactor NEDÁ

**Kompilátor zahození výsledku nenahlásí.** C# nemá `[[nodiscard]]`, CA1806 se
vztahuje jen na `[Pure]` metody a tyhle pure nejsou (zapisují do DB), takže je tak
označit by byla lež. Přínos je jinde: chyba nese **text pro uživatele** a volající,
který ji ignoruje, je při čtení zjevně vadný. Vynucení musí zajistit testy a review,
ne typ.

> Zapsáno záměrně — `next-wave-plan.md` psal, že „compiler pak zahození výsledku
> umí nahlásit“. To není pravda a je lepší to vědět předem než se na to spoléhat.

### Postup

1. `Result` doplnit o `Fail` s výjimkou pro logování a o implicitní `bool`? **Ne** —
   implicitní konverze na `bool` by vrátila přesně ten problém, který řešíme.
2. Službu po službě: signatura → volající → testy → zelená sada.
3. Chybové texty česky, protože jdou do `Snackbar` uživateli.

### Stav

- [x] `Result` doplněn o `ResultErrorKind` (`Failure` / `NotFound` / `Rejected`).
      Bez toho by controller nerozlišil 404 od 500 a musel by hádat z textu.
- [x] Všech 6 služeb: `CreateAsync`/`CreateOrUpdateAsync` → `Result<int>` s klíčem,
      `DeleteAsync` → `Result`. Sjednoceno i to, že chybějící řádek je `NotFound`,
      ne výjimka — `EventService.DeleteAsync` se dřív chovala jinak než ostatní.
- [x] Controllery + `ResultHttpExtensions.ToErrorResponse()` jako jedno místo
      pro mapování na HTTP kód.
- [x] Razor stránky: chybové texty jdou do `Snackbar` z `Result.Error`.
- [x] Testy přepsané, `ResultAssert` vypisuje `Error` při selhání.
- [x] `AuthenticationService` / `AuthController` — bez změny, používají
      `RefreshTokenService`, který do vzoru nepatří.

**269 testů zelených** (106 unit + 163 integration).

### Co se při tom našlo

| Místo | Co se dělo |
|---|---|
| `AttendancesController` (4 akce) | `await CreateOrUpdateAsync(...)` bez kontroly a pak `Ok(dto)` — mobil ukázal docházku jako uloženou, i když nebyla |
| `FilesController`, `DancesController` upload | odmítnutí kvótou skončilo jako HTTP 200 s DTO souboru, který v DB není |
| `ListEvents.RemoveEvent` | `Events.RemoveAll(...)` bez ohledu na výsledek — řádek zmizel z mřížky, v DB zůstal |
| `ListDances`, `ListMembers`, `ListVideoLinks` | smazání i vytvoření bez kontroly, mřížka se jen obnovila |
| `FileService` kvóta | důvod zamítnutí se zahazoval; teď jde jako `Rejected` až do UI |
| `AttendancesController` GCal | událost se zakládá před uložením → při neúspěchu osiřela; nově se ruší |

---

## 2. E2E infrastruktura

**Volba: Playwright pro .NET** (`Microsoft.Playwright` 1.56). Důvody:

- Blazor Server je SignalR nad DOM — bez skutečného prohlížeče se interakce
  netestuje. `WebApplicationFactory` umí jen HTTP, což už `AuthApiTests` dělají.
- Jede headless v CI, stejný jazyk i runner jako zbytek sady.
- Chromium je jediný prohlížeč, který nainstaluji — na víc není důvod, appka
  necílí na Safari.

Ověřeno 2026-09-09: balíček se obnoví, `playwright.ps1 install chromium` projde.

**Host pro E2E:** ne `WebApplicationFactory` (`TestServer` nemá skutečný socket,
takže na něj prohlížeč nepřipojí). Místo toho `WebApplication` na reálném Kestrelu
na volném portu, pod jedním fixture na celou sadu.

### Stav

- [x] Projekt `Demizon.Tests.E2E` a **vlastní filtr `Demizon.E2E.slnf`** — ne
      `Demizon.Backend.slnf`. Rychlá sada tak zůstává bez závislosti na prohlížeči
      a v CI je to samostatná úloha, která si Chromium doinstaluje.
- [x] `AppHost` spouští aplikaci jako **samostatný proces** na reálném Kestrelu
      (`dotnet Demizon.Mvc.dll`, ne `dotnet run` — ten bere URL z `launchSettings`),
      s temp SQLite, čeká na `/health`.
- [x] Seed admina a člena přímo do SQLite (přes API by to nešlo: člena zakládá
      jen admin a bootstrap endpoint se po prvním použití zamkne).
- [x] `E2EFixture` drží jeden host a jeden prohlížeč na celou sadu; každý test
      má vlastní `BrowserContext`, takže se testy navzájem nepřihlašují.
- [x] Chybějící Chromium hlásí přesný příkaz k instalaci, ne nesouvisející timeout.

**23 E2E testů, běh 24 s.**

---

## 3. E2E scénáře

Priorita podle toho, co rozbití nejvíc bolí a co nižší vrstvy nevidí:

- [x] `AuthFlowTests` (6): správné heslo → `/Admin`, špatné i neznámý login →
      `/Login`, nepřihlášený admin obsah nevidí, odhlášení přístup zavře,
      běžný člen se přihlásí taky. Cookie cesta, kterou `AuthApiTests` (JWT)
      nepokrývají.
- [x] `PublicPageTests` (13): 6 veřejných stránek × (vykreslení bez chyby
      v konzoli + žádný vodorovný přesah) + naběhnutí Blazor okruhu.
- [x] `MudBlazorLayoutTests` (4): nulové rozměry interaktivních prvků,
      přetékání na 390 px u veřejných stránek i administrace.

---

## 3b. Doplněné díry v unit/integračních testech

Vzato z TODO v [`testing-plan.md`](testing-plan.md), sekce „Testy, které záměrně
nejsou“ — tyhle tři už tam nepatří:

- [x] **HTTP 429 na `/api/auth/token`.** Ostatní testy mají limit zvednutý, aby
      se do něj suite netrefila, takže ho nikdo neověřoval — a přitom chrání
      proti hádání hesel. `TunedApiFactory` staví host s vlastním limitem;
      druhý test hlídá, že vyčerpané okno neblokuje endpointy bez politiky
      `auth` (`/health`).
- [x] **Strop alokátoru ImageSharpu.** Chování bylo pokryté (test si strop
      lokálně snižuje), ale to, že ho `AddCoreServices` vůbec nastaví, nepokrýval
      nikdo — jeho odstranění by žádný test nezachytil. Konkrétní hodnotu
      ImageSharp nezveřejňuje, takže se ověřuje záměna instance.
- [x] **Kontrakt uploadu při plné kvótě** — 400 s důvodem a nic v databázi.

> **Poučení k tomu poslednímu.** Napsal jsem ho jako regresní test k Result
> refactoringu ve `FilesController` a spustil ho proti kódu bez opravy —
> **prošel**. Kvóta se totiž kontroluje dvakrát: ve `FileUploadService` a pak
> ještě ve `FileService.CreateAsync`. Přes HTTP se vždy uplatní ta první, protože
> obě sčítají totéž, takže druhá je obrana do hloubky, na kterou se z endpointu
> nedá dostat. Test tedy hlídá kontrakt endpointu, ne tu opravu; kontrolu
> návratové hodnoty pokrývá až test na úrovni služby, kde se navíc ověřuje
> `ResultErrorKind.Rejected` a že důvod nese text.
>
> Bez toho ověření by v dokumentaci zůstalo tvrzení, které neplatí.

**273 testů v rychlé sadě** (112 unit + 163 integration) + 23 E2E.

---

## 4. Vizuální QA MudBlazoru

Pixel-diff baseline je napříč stroji křehká (fonty), takže **ne** jako tvrdá
assertion. Místo toho:

- [x] **Automatizované strukturální kontroly** v `MudBlazorLayoutTests`
      a `PublicPageTests`.
      > Jedno selhání při prvním běhu bylo poučné: kontrola nulových rozměrů
      > našla odkaz „Reload“ z Blazorova reconnect dialogu. Nebyla to chyba
      > aplikace, ale testu — `getComputedStyle` na samotném prvku `display:none`
      > nevidí, protože skrytý je jeho rodič. Správný nástroj je
      > `Element.checkVisibility()`, které bere v potaz i předky.
- [x] **Screenshoty jako artefakt** — desktop i 390 px, ukládají se do
      `e2e-artifacts/` a CI je vystavuje jako artefakt běhu.
- [x] **Claude in Chrome** — proklikáno 2026-09-09 nad běžící aplikací
      (přihlášení, členové, akce, tance, statistiky, profil, docházka, dialogy).

### Nálezy z ručního proklikání

| # | Nález | Stav |
|---|---|---|
| 4 | **Formulář akce šel uložit bez termínu.** `ClickedOk` zavíral dialog bez jakékoli validace — `Required="true"` na poli jen vykreslí hvězdičku, nic neblokuje — a `EventViewModel.ToEntity()` pak spadlo na `Date.Start!.Value`. Uživatel viděl obecné „Něco se pokazilo.“ | ✅ opraveno: `MudForm` + validace v `ClickedOk`, `Required` na `MudDateRangePicker`; hlídá `Akci_nejde_ulozit_bez_terminu` (padá bez opravy) |
| 5 | Ten `catch` výjimku **spolkl bez zalogování**, takže v logu nezůstala stopa a nebylo co dohledat | ✅ `Logger.LogError` na obou místech v `ListEvents` |
| 6 | **Žádný formulář nepoužíval `MudForm`**, takže ani jeden nevalidoval před zavřením | ✅ opraveno u všech, které mají co validovat: `EventForm`, `DanceForm`, `VideoLinkForm`, `MemberForm`. Hlídá `Formular_nejde_ulozit_s_nevyplnenymi_povinnymi_poli` (4 případy; bez validace padnou právě ty tři nové) |

**K bodu 6 dvě rozhodnutí, která nejsou mechanická:**

- **`AttendanceForm` zůstává bez `MudForm` záměrně** — nemá povinné pole.
  Radio group má vždy hodnotu, role i poznámka jsou nepovinné, takže by to
  byl jen ceremoniál.
- **`Required` na `VideoLinkForm.Year` nic neznamenalo** — `Year` je `int`
  a nula je taky hodnota, takže povinnost nešla porušit ani splnit. Nahrazeno
  rozsahovou kontrolou 1900–2100, která prázdné i nesmyslné pole zachytí.
- `MemberForm` si své ruční kontroly (shoda hesel, formát e-mailu) drží dál —
  `Required` je nepokrývá.

Při té příležitosti smazán `DanceNumberForm.razor`: 84 bajtů obsahující jediný
komentář „Removed – DanceNumber concept has been replaced by standalone Dance
entities“, nula referencí.

**Ověřeno jako funkční** (ne nález): datum v administraci je česky
(`Září 2026`, `01.01.2026`), dialogy se otevírají i zavírají, MudBlazor
providery na stránce jsou (snackbar se ale jmenuje
`mud-snackbar-location-top-right`, ne `mud-snackbar-provider` — moje původní
E2E assertion na názvy tříd byla proto příliš slabá), a celý průchod
„vytvořit tanec“ funguje včetně `Result` → snackbar „Tanec byl vytvořen.“
→ obnovení mřížky. Tím je Result refactor ověřený i v reálném UI.

> **Falešný poplach, který stojí za zapsání.** Klik na „Vytvořit“ přes
> `computer left_click` s `ref` dialog neotevřel a chvíli to vypadalo na
> chybějící `MudDialogProvider`. Ověření v DOMu ukázalo, že providery tam jsou
> a dialog se po programovém `element.click()` otevře — nedorazil tedy klik,
> ne aplikace. Bez toho ověření bych nahlásil neexistující chybu.

### Průchod administrací (`AdminWalkthroughTests`)

9 stránek administrace × (desktop kontroly + mobil 390 px), plus kontrola
MudBlazor providerů a otevření/zavření dialogu. **48 E2E testů celkem.**

### Nalezené vizuální chyby

| # | Nález | Stav |
|---|---|---|
| 1 | `/Admin/Members` přetékal na 390 px o 9 px — toolbar tabulky nese vyhledávání, tři filtry, refresh i „Vytvořit“ v jedné nezabalitelné řádce | ✅ opraveno v `site.css` (`.mud-table-toolbar { flex-wrap: wrap; height: auto }`), řeší celou třídu pro všechny admin tabulky |
| 2 | **Žádná** admin tabulka neměla `DataLabel`, takže na telefonu se buňky naskládaly pod sebe bez popisků a nešlo poznat, co je co | ✅ doplněno 31 popisků v 5 tabulkách + test `Bunky_tabulky_maji_na_mobilu_popisek` |
| 3 | Datum v administraci se vypisuje anglicky („1. June 2026“) | ⬜ **neopraveno záměrně** — viz níže |

**K bodu 3.** `Program.cs` má `supportedCultures = ["en-US", "cs-CZ"]` a
`SetDefaultCulture(supportedCultures[0])`, takže návštěvník **bez culture cookie**
dostane en-US. Administrace má být podle strategie česky (viz paměť projektu:
CZ/EN jen pro veřejné stránky, admin zůstává český), takže je to rozpor. Změna
výchozí kultury ale ovlivní i veřejné stránky, kde je dvojjazyčnost záměr —
to je produktové rozhodnutí, ne úklid. Možnosti: přehodit výchozí kulturu na
`cs-CZ`, nebo ji vynutit jen v `AdminMainLayout`.

> **Poučení k metodě.** Nález 1 našla strukturální kontrola, nález 2 a 3 až
> **pohled na screenshot**. Stránka nepřetékala, prvky měly rozměry, konzole
> mlčela — a tabulka členů byla na telefonu přesto nečitelná. Automatizace
> a lidské oko tu nejsou náhrady, ale doplňky: první hlídá regrese, druhé
> najde to, co nikdo neumí předem vyjádřit jako assertion.

---

## 5. Code review

Dvě kola: vlastní revize a nezávislá přes `/code-review`.

### Vlastní revize — 3 nálezy, všechny opravené

**1. Porušený kontrakt „služba nikdy nevyhodí výjimku“** (moje regrese).
Abych odlišil „nenalezeno“ od chyby zápisu, vytáhl jsem v `DeleteAsync`
vyhledání entity **mimo** `try` — v pěti službách. Volající (Razor stránky
i controllery) kolem těch volání `try/catch` nemají, protože se spoléhají na to,
že chyba přijde jako návratová hodnota. Výjimka při čtení (`SQLITE_BUSY` není
na jednom vCPU s WAL hypotéza) by prolétla do Blazor okruhu a uživateli by
zhasla stránka. Vyhledání je zpátky v `try`, nenalezení zůstává hodnotou.

**2. Záchranná pomůcka sama vyhazovala výjimku** (starší latentní chyba, kterou
odhalil až test k nálezu 1). `DiscardPendingChanges` sahá na
`context.ChangeTracker`, což na zavřeném kontextu hodí
`ObjectDisposedException` — a volající ji používají **uvnitř** `catch` bloku,
takže zotavení hodilo novou výjimku a kontrakt zmařilo bez ohledu na nález 1.
V Blazor Serveru je kontext scoped na celý okruh, takže uložení dorazivší po
zavřeném okruhu je reálný scénář. Chytá se **jen** `ObjectDisposedException`
(kontext je pryč → tracker s ním → není co uklízet), cokoli jiného propadne dál.

**3. Neomezený buffer výstupu v E2E hostu.** Sbíral se jen pro diagnostiku
selhání startu, ale v Development loguje host každý EF dotaz — za 48 testů
desetitisíce řádků v paměti. Kruhová fronta na 200 řádků.

`ServiceExceptionContractTests` hlídá 1 i 2 (běh služeb nad zavřeným
kontextem) a k tomu to, že `CreateOrUpdateAsync` po neúspěchu nechá `Value`
na nule — volající se rozhoduje podle `Id != 0`. **Tři testy padaly před
opravou.**

> **Poučení.** Nález 1 je přesně ta chyba, na kterou je vlastní revize dobrá:
> šlo o *úmyslné* zlepšení (odlišit 404 od 500), které mimoděk porušilo
> nevyslovený kontrakt. A nález 2 se ukázal jen proto, že jsem k nálezu 1
> napsal test — bez něj bych opravil symptom a hlubší chybu nechal ležet.

### Nezávislá revize (`/code-review 2770d60..HEAD high`)

Nezávisle našla **stejný hlavní problém** jako vlastní revize (vyhledání mimo
`try` i díru v `DiscardPendingChanges`) — mezitím už opravený — a k tomu
**8 dalších nálezů. Všechny platné, všechny opravené.**

Ověřila taky dvě věci, které vypadají špatně a nejsou:
`catch (TimeoutException)` v E2E míří na `System.TimeoutException`, což
Playwright 1.56 skutečně vyhazuje (vlastní typ nemá), a `site.css` se načítá
za `MudBlazor.min.css`, takže nové pravidlo pro toolbar vyhraje.

| # | Nález | Oprava |
|---|---|---|
| 1 | `CoreRegistrationTests` a `FileUploadServiceImageTests` obě přepisují **globální** `ImageSharp.Configuration.Default.MemoryAllocator` a xUnit je pouští paralelně. Dvě konkrétní interleavings: 128MB alokátor uprostřed dekódování rozbije test „nad stropem“, nebo si registrační test uloží 16MB strop jako `original` a propíše ho do zbytku běhu | kolekce `ImageSharpGlobals` obě serializuje |
| 2 | `Dialog_se_otevre_a_zavre` klikal na `button:has(.mud-icon-root).First`, což je **přepínač navigačního šuplíku**. Dialog se nikdy neotevřel, timeout spolkl `catch` a test procházel naprázdno — chybějící `MudDialogProvider`, tedy přesně ta regrese, kterou má hlídat, by ho nerozbila | cílení přes `GetByRole(Button, "Vytvořit")`, zrušené `return`y |
| 3 | Doplnění `DataLabel` minulo **šestou** tabulku (`AttendanceStats`) a `TablePages` tu routu neobsahoval, takže by to CI nezachytilo | 3 popisky + route do testu |
| 4 | Na cestě „přepnu na nepřijdu“ se událost smaže z kalendáře, ale `GoogleEventId = null` existuje jen v paměti; když uložení selže, `DiscardPendingChanges` to vrátí a v databázi zůstane ID neexistující události. A protože se nová událost zakládá **jen** při prázdném `GoogleEventId`, příští „přijdu“ už mlčky žádnou nevytvoří. Natrvalo | nový `ClearGoogleEventIdAsync` (cílený `ExecuteUpdateAsync` mimo change tracker, protože se volá po zahozeném trackeru); zapojen ve 3 API akcích i v Blazor stránce |
| 5 | `ResultHttpExtensions` slibuje klientům vždy tvar `{ error }`, ale sousední selhání na témže endpointu vracela prosté stringy | sjednoceno na `{ error }` |
| 6 | `.mud-table-toolbar { min-height: 64px }` přebíjelo `--mud-internal-toolbar-height`, které je u `Dense="true"` 48 px — `ListPhotos` a `AttendanceStats` by narostly o 16 px | `min-height: var(--mud-internal-toolbar-height)` |
| 7 | `Result.Ok(...)` nesl `ErrorKind.Failure`, tedy „mapuj na 500“. Jedno omylem zavolané `ToErrorResponse` na úspěchu = HTTP 500 s `{ error: null }` — přesně ta past, kterou má `Result` odstraňovat | přidán `ResultErrorKind.None = 0`; `ToErrorResponse` na úspěchu vyhodí `InvalidOperationException` místo tiché 500 |
| 8 | Kontraktový test „služba nikdy nevyhodí“ vynechal `FileService` — jedinou postavenou na `ExecuteDeleteAsync`, tedy jdoucí jinou cestou | doplněn do obou testů |

> **Poučení k nálezu 2.** Je to učebnicový případ toho, před čím varuje
> `testing-plan.md`: test, který po opravě prochází, ale opravu neprochází.
> Zelená barva sama nic neznamená — u testu, který má chytat regresi, je
> potřeba ho vidět zčervenat.

**330 testů**: 112 unit + 169 integration + 49 E2E.

---

## Poznámky k prostředí

- `dotnet test Demizon.slnx` neprojde (MAUI chce `maui-android`) → vždy
  `Demizon.Backend.slnf`.
- `dotnet run` bere URL z `launchSettings.json`, `ASPNETCORE_URLS` ignoruje.
- V `Development` **není** zaregistrovaný exception handler ani dev exception page:
  neošetřená výjimka = HTTP 500 s prázdným tělem a bez logu.
- Validační atributy na `record` patří na parametry primárního konstruktoru,
  ne na property (.NET 10 jinak hodí při stavbě metadat akce).
