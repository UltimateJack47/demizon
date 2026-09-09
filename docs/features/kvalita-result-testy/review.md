# Code review vlny kvality

> ✅ **Uzavřeno 2026-09-09.** Záznam nálezů, ne seznam úkolů.
> Kontext a zadání vlny: [`README.md`](README.md).

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
