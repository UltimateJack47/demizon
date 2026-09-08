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
| 2 | Result refactor: služby + volající + testy | ⏳ probíhá |
| 3 | E2E infrastruktura (Playwright) | ⬜ čeká |
| 4 | E2E scénáře | ⬜ čeká |
| 5 | Doplnit unit/integrační díry | ⬜ čeká |
| 6 | Vizuální QA MudBlazor 9.9 | ⬜ čeká |
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

- [ ] `Result` doplnit o to, co refactor potřebuje (viz Postup)
- [ ] `AttendanceService` (+ 4 controller volání, `MemberAttendance.razor.cs`)
- [ ] `DanceService`, `EventService`, `FileService`, `MemberService`, `VideoLinkService`
- [ ] Razor stránky: `ListDances`, `ListEvents`, `ListMembers`, `ListPhotos`,
      `ListVideoLinks`, `Dance/Detail`, `MemberForm`
- [ ] Controllery: `Attendances`, `Dances`, `Events`, `Files`, `Videos`, `Auth`
- [ ] `AuthenticationService`
- [ ] Testy přepsané na nový kontrakt

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

- [ ] Projekt `Demizon.Tests.E2E` + `Demizon.Backend.slnf`
- [ ] Kestrel fixture + čekání na `/health`
- [ ] Seed známého admina a člena
- [ ] Skip, když Chromium není nainstalovaný (ať CI bez browseru nepadá)

---

## 3. E2E scénáře

Priorita podle toho, co rozbití nejvíc bolí a co nižší vrstvy nevidí:

- [ ] Přihlášení: správné heslo → admin, špatné → chybová hláška
- [ ] Přesměrování nepřihlášeného z admin stránky na login
- [ ] Odhlášení zneplatní přístup do administrace
- [ ] Veřejné stránky se vykreslí bez chyby v konzoli
- [ ] Blazor okruh naběhne (SignalR) a stránka reaguje na interakci
- [ ] Admin: seznam členů se načte a filtr reaguje
- [ ] Docházka: přepnutí stavu se uloží a přežije reload

---

## 4. Vizuální QA MudBlazoru

Pixel-diff baseline je napříč stroji křehká (fonty), takže **ne** jako tvrdá
assertion. Místo toho:

- [ ] **Automatizované strukturální kontroly** v E2E: žádný vodorovný přesah
      stránky, žádné nulové/nezobrazené interaktivní prvky, dialog se zavře,
      snackbar se objeví. To jsou třídy MudBlazor regresí, které build nezachytí.
- [ ] **Screenshoty jako artefakt** (desktop + mobil), ne assertion — k prohlédnutí
      člověkem a k porovnání při příštím upgradu.
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
