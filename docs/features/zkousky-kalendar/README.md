# Kalendář zkoušek: jedno pravidlo, rušení a výjimky

> Založeno: 2026-09-30. Stav: 🚧 **implementace probíhá** (od 2026-10-01), stav
> po krocích v [Postupu](#postup). Všechny otázky rozhodnuté 2026-09-30
> (viz [Rozhodnutí](#rozhodnutí)).

## Zadání

1. Pravidlo „každý pátek je zkouška, pokud tam není akce“ má žít **na jednom místě**.
2. Jednu zkoušku (a celé období, např. prázdniny) má jít **zrušit**.
3. **Vícedenní akce** má schovat zkoušku v pátek, přes který vede, ne jen když v pátek začíná.
4. **Virtuální výskyty zůstávají.** Zkoušky se dál nepředgenerují do DB, řádek
   `Attendance { EventId = null, Date = pátek }` vzniká až vyplněním docházky.
   Rušení se řeší malou tabulkou výjimek.

## Současný stav (2026-09-30)

Pravidlo je zkopírované pětkrát a kopie se už rozcházejí:

| Místo | Kontroluje akci v pátek? | Poznámka |
|---|---|---|
| `Demizon.Mvc/Controllers/Api/AttendancesController.cs:479` `GetMonthlyTable` | ano, `DateFrom.Date` | měsíční tabulka (Flutter, MAUI) |
| `Demizon.Mvc/Controllers/Api/EventsController.cs:71` `GetByMonth` | ano, `DateFrom.Date` | zkouška má `DateTo = pátek 00:00 + 2 h` |
| `Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor.cs:97` `LoadData` | ano, `DateFrom.Date` | Blazor admin |
| `Demizon.Mvc/Services/Notification/UnifiedNotificationService.cs:244` `CheckRehearsalRemindersAsync` | ano, `DateFrom.Date` | automatické připomínky 5/3/1 den |
| `Demizon.Mvc/Services/Notification/AttendanceReminderService.cs:82` `NotifyMissingRehearsalAttendanceAsync` | **ne** | ruční připomínka pošle „zkouška“ i na pátek s vystoupením |

Další nálezy:

- **Uložení docházky nic nevaliduje.** `UpsertRehearsal` (`AttendancesController.cs:153`)
  a `UpsertMemberRehearsal` (`:351`) přijmou libovolné datum, klidně úterý.
- **Časová zóna.** ~~`DateFrom` je v UTC~~ — *opraveno při implementaci 2026-10-01:*
  časy akcí jsou **místní** (Europe/Prague), ne UTC. `UtcDateConvertExtension`
  existuje, ale nikde se nevolá; Flutter posílá ISO bez `Z`, Blazor date picker
  půlnoc a `GoogleCalendarService.ToLocalOffset` čte `DateFrom` jako místní čas.
  `.Date` je tedy správně a převod UTC → Praha by chybu naopak zavedl
  (akce v pátek 23:00 by se posunula na sobotu).
- **Konec akce v 00:00 má dva významy.** Blazor zadává jen datum od–do, obojí
  o půlnoci, a poslední den do akce *patří* („čt–pá“ má `DateTo = pá 00:00`).
  Akce s časem končící o půlnoci („čt 18:00 – pá 00:00“) do pátku nezasahuje.
  `RehearsalSchedule.CoveredDays` je rozlišuje podle toho, jestli jsou o půlnoci
  oba konce.
- **`IsEvent` v `MonthlyColumnDto`** se počítá jako `Recurrence != Weekly`. Týdně se
  opakující akce se tedy tváří jako zkouška. Vypadá to jako pozůstatek dřívějšího
  modelování zkoušek přes `Event`. Při refaktoru rozhodnout, co s tím.
- **Google Calendar.** Kdo má docházku „ano“ a propojený kalendář, má v Google
  událost „Zkouška Demizon“. Když se na ten pátek dnes založí akce, událost v Google
  zůstane viset. Po zavedení rušení by to platilo i pro zrušenou zkoušku.

## Návrh

### 1. Čistý výpočet: `RehearsalSchedule`

Statická třída v `Demizon.Core` **bez přístupu k DB**. Díky tomu se dá pokrýt unit
testy bez fixtury.

```csharp
public enum RehearsalState { Scheduled, Cancelled }

public sealed record RehearsalOccurrence(
    DateOnly Date,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    RehearsalState State,
    int? CancellationId,
    string? CancellationReason);

public static class RehearsalSchedule
{
    // [from, toExclusive), data už načtená volajícím
    public static IReadOnlyList<RehearsalOccurrence> Compute(
        DateOnly from, DateOnly toExclusive,
        RehearsalRule rule,                       // den v týdnu, začátek, délka, TimeZoneInfo
        IEnumerable<EventSpan> events,            // (DateFrom, DateTo, IsCancelled) v UTC
        IEnumerable<CancellationSpan> cancellations);
}
```

Pravidla výpočtu:

1. Vezmi každý den v rozsahu, který je `rule.DayOfWeek`.
2. **Akce ho schová**, když jde přes ten den: `start.Date <= den <= konec.Date`.
   Časy jsou už místní (viz *Současný stav*), nic se nepřevádí. Akce s časem
   končící přesně v 00:00 do toho dne nezasahuje; akce zadaná jen datem
   (oba konce o půlnoci) má poslední den včetně. Schová ho **každá nezrušená
   akce**, i malé vystoupení. **Zrušená akce zkoušku vrátí.**
3. **Výjimka ho nesmaže, ale označí.** Zrušená zkouška se vrátí se
   `State = Cancelled`, aby ji členové viděli jako „odpadá“ a nemizela jim beze stopy.
4. `StartsAt`/`EndsAt` (místní, jako `Event.DateFrom`) a `StartsAtUtc`/`EndsAtUtc`
   se spočítají z `rule`. Tím odpadne
   `DateTo = pátek 00:00 + 2 h` v `GetByMonth`.

`RehearsalRule` se naplní z `appsettings` (sekce `Rehearsals`: `DayOfWeek = Friday`,
`StartTime = 19:00`, `Duration = 02:30`). Časová zóna se převezme z `GoogleCalendar:TimeZone`.
Tu je lepší časem přesunout do obecné sekce.

### 2. Služba: `IRehearsalService`

`Demizon.Core/Services/Rehearsal/`. Načte akce a výjimky v jednom dotazu na rozsah
a zavolá `RehearsalSchedule.Compute`.

```csharp
Task<IReadOnlyList<RehearsalOccurrence>> GetRangeAsync(DateOnly from, DateOnly toExclusive, CancellationToken ct);
Task<RehearsalOccurrence?> GetAsync(DateOnly date, CancellationToken ct);   // null = v ten den zkouška není

// správa výjimek (admin)
Task<Result<RehearsalCancellation>> CancelAsync(DateOnly from, DateOnly to, string? reason, int byMemberId, CancellationToken ct);
Task<Result> RestoreAsync(int cancellationId, CancellationToken ct);
Task<IReadOnlyList<RehearsalCancellation>> GetCancellationsAsync(DateOnly from, DateOnly toExclusive, CancellationToken ct);
```

Pro výsledky se použije stávající kontrakt `Result` (viz
[`kvalita-result-testy`](../kvalita-result-testy/README.md)).

### 3. Tabulka výjimek: `RehearsalCancellation`

| Sloupec | Typ | Poznámka |
|---|---|---|
| `Id` | int | |
| `DateFrom` | date | včetně |
| `DateTo` | date | včetně; jedna zkouška = `DateFrom == DateTo` |
| `Reason` | string? | „Prázdniny“, „Hody v Lanžhotě“… zobrazí se členům |
| `CreatedAt` | DateTime (UTC) | |
| `CreatedByMemberId` | int? | FK na `Member`, `SetNull` |

Jedna tabulka pokryje jednotlivou zkoušku i celé prázdniny. Stará docházka na
zrušený den **se nemaže.** Obnovením výjimky se vrátí, jak byla. Dokud je výjimka
aktivní, jen se nezobrazuje a nepočítá.

### 4. Chování napříč systémem

| Místo | Po změně |
|---|---|
| Měsíční tabulka (`GetMonthlyTable`) | sloupec zůstane s `IsCancelled = true`, v popisku důvod. Flutter i MAUI `IsCancelled` už vykreslují (šedé „–“) |
| Seznam měsíce (`GetByMonth`) | `EventDto { IsRehearsal = true, IsCancelled = true }`, skutečné časy zkoušky |
| Blazor admin `MemberAttendance` | stejný zdroj, zrušená zkouška bez možnosti vyplnit |
| Automatické připomínky | zrušené přeskočí, vícedenní akce opravené díky společnému výpočtu |
| Ruční připomínka | pro zrušenou zkoušku `Cancelled`, pro den bez zkoušky `NotFound`, a to i když na ten pátek připadá akce |
| `UpsertRehearsal`, `UpsertMemberRehearsal` | 400 „V tento den zkouška není“ nebo 409 „Zkouška je zrušená“ |
| Google Calendar | při zrušení smazat události „Zkouška Demizon“ členům s docházkou „ano“ |
| Push | **volitelný**: při zrušení zaškrtávátko „Upozornit členy“ (výchozí vypnuto). Stejné zaškrtávátko dostane i zrušení akce, které dnes (`EventsController.ToggleCancelled`) nepošle nic. U zrušené páteční akce text zmíní, že místo ní je zkouška |

DTO se jen **rozšíří** o nepovinné pole `CancellationReason`. Starší klienti ho
ignorují, kontrakt se nerozbije.

## Postup

Kroky jdou po sobě a každý projde testy samostatně. Refaktor (A–B) nemění
chování kromě dvou opravených chyb. Nová funkce přichází až v C.

- [x] **A. `RehearsalSchedule` + unit testy.** (2026-10-01, `RehearsalScheduleTests`, 19 testů.
      Případ „pátek 00:30 = UTC čtvrtek“ se díky místním časům změnil na „pátek 00:30 je pátek“.) Případy: měsíc s 4 a 5 pátky, hranice
      měsíce, jednodenní akce v pátek, vícedenní akce čt–ne, akce končící v 00:00,
      akce v pátek 00:30 místního času (UTC čtvrtek), zrušení jednoho dne, zrušení
      rozsahu přes hranici měsíce, přechod letního času (začátek zkoušky v UTC).
- [x] **B. `IRehearsalService` a přepojení všech pěti míst** na `GetRangeAsync`
      a `GetAsync`. Integrační testy pro API (měsíční tabulka, `GetByMonth`)
      a obě připomínky. Opraví se tím ruční připomínka na pátek s akcí
      a vícedenní akce. (2026-10-01: `RehearsalServiceTests` nad SQLite,
      `RehearsalApiTests` přes HTTP. Navíc: čas zkoušky je jen v sekci
      `Rehearsals`, `GoogleCalendar:RehearsalStartHour/EndHour` zmizely
      a Google událost dostane 19:00–21:30. `IsEvent` = `EventId != null`.
      Blazor tabulka bere `EndDate` jako konec měsíce, ne půlnoc posledního dne,
      takže už nevypadne akce 31. večer.)
- [x] **C. Validace uložení docházky** v obou `Upsert*Rehearsal` přes `GetAsync`.
      Zkontrolovat, že Flutter posílá jen dny, které dostal od API.
      (2026-10-01: `RejectUnlessRehearsalAsync`, běží před synchronizací s Google.
      Flutter bere datum ze sloupce tabulky, z `event.dateFrom` nebo z payloadu
      notifikace, tedy vždy od API. Mazání docházky validaci nemá záměrně, aby
      šla uklidit i docházka na den, který zkouškou přestal být.)
- [ ] **D. Migrace `RehearsalCancellation`** + `CancelAsync` / `RestoreAsync` /
      `GetCancellationsAsync`. API pro admina: `GET/POST /api/rehearsals/cancellations`,
      `DELETE /api/rehearsals/cancellations/{id}`.
- [ ] **E. Admin UI v Blazoru**: seznam výjimek, formulář od–do + důvod,
      obnovení výjimky. Rychlá akce „Zrušit tuhle zkoušku“ přímo ze sloupce
      měsíční tabulky.
- [ ] **F. Důsledky zrušení**: úklid událostí v Google Calendar (stejná kompenzace
      jako u `ReconcileCalendarAfterFailedSaveAsync`). Volitelný push (`notify` v požadavku)
      pro zrušení zkoušky i akce, nový `NotificationType` pro obojí, aby šel dohledat
      v `SentNotifications`. Úklid Google
      událostí se týká i existující chyby „akce založená na pátek s vyplněnou
      zkouškou“ a měl by jet stejnou cestou.
- [ ] **G. Flutter**: zobrazit důvod zrušení, zakázat vyplnění. Admin akci zrušení
      v appce jen pokud bude chtít (Blazor admin stačí).
- [ ] **H. Dokumentace**: `notifications.md` (připomínky přeskakují zrušené zkoušky),
      `STATUS.md`, tady stav kroků.

## Rozhodnutí

Rozhodnuto 2026-09-30:

1. **Zkouška začíná v 19:00 a trvá 2 h 30 min.** Dosud to v kódu nebylo, jen lokalizovaný
   text `EveryFridayEvening`. Google Calendar dnes zakládá celodenní událost, po
   změně dostane skutečný čas.
2. **Zrušená akce zkoušku vrátí.** Dnes ji schová i zrušená akce. Kdo nechce ani
   zkoušku, přidá výjimku.
3. **Zkoušku schová každá akce**, i malé vystoupení. Příznak `ReplacesRehearsal` se nezavádí.
4. **Push při zrušení je volitelný**, zaškrtávátko u zrušení zkoušky i akce, výchozí vypnuto.
5. **Push dostanou ti, kdo nemají „ne“**, tedy „ano“, „nevím“ a kdo se ještě nevyjádřil.
   Platí stejně pro zkoušku i akci.
6. **Mimořádné zkoušky v jiný den nejsou.** Vícedenní soustředění se zakládá jako akce.
   Tabulka výjimek tedy umí jen rušit, `Kind = Extra` se nezavádí.
7. **Týdně se opakující akce se nepoužívají.** Jediné, co se opakuje, jsou virtuální zkoušky.
   `IsEvent` v `MonthlyColumnDto` bude prostě `EventId != null`. `Event.Recurrence`
   a `RecurrenceEndDate` jsou kandidáti na odstranění, ale až samostatně: pole nese
   i `EventDto` a Flutter/MAUI ho čtou.

Otevřené otázky nezbývají.
