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
| 6 | Vizuální QA MudBlazor 9.9 | ⏳ probíhá |
| 7 | Code review celé vlny | ⬜ čeká |

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
- [ ] **Claude in Chrome** na ruční proklikání administrace nad běžící appkou.

---

## 5. Code review

Nad celým diffem vlny, se zaměřením na: zapomenuté ignorované `Result`, chybové
texty, flaky E2E (čekání na stav místo `sleep`), a jestli testy skutečně chytají
regresi (spustit proti kódu bez opravy).

---

## Poznámky k prostředí

- `dotnet test Demizon.slnx` neprojde (MAUI chce `maui-android`) → vždy
  `Demizon.Backend.slnf`.
- `dotnet run` bere URL z `launchSettings.json`, `ASPNETCORE_URLS` ignoruje.
- V `Development` **není** zaregistrovaný exception handler ani dev exception page:
  neošetřená výjimka = HTTP 500 s prázdným tělem a bez logu.
- Validační atributy na `record` patří na parametry primárního konstruktoru,
  ne na property (.NET 10 jinak hodí při stavbě metadat akce).
