# Kalendář zkoušek: jedno pravidlo, rušení a výjimky

> Založeno: 2026-09-30. Stav: ✅ **hotové v kódu** (2026-10-01), kroky A–H
> v [Postupu](#postup). Flutter část čeká na ověření na telefonu spolu se zbytkem
> appky. Všechny otázky rozhodnuté 2026-09-30 (viz [Rozhodnutí](#rozhodnutí)).
>
> Sekce *Současný stav* a *Návrh* popisují stav **před** implementací. Kde se
> implementace od návrhu odchýlila, je to poznamenané u kroku v *Postupu*.

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
| `UpsertRehearsal`, `UpsertMemberRehearsal` | 400 „V tento den zkouška není“ nebo 400 „Zkouška je zrušená“ |
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
- [x] **D. Migrace `RehearsalCancellation`** + `CancelAsync` / `RestoreAsync` /
      `GetCancellationsAsync`. API pro admina: `GET/POST /api/rehearsals/cancellations`,
      `DELETE /api/rehearsals/cancellations/{id}`.
      (2026-10-01: migrace `AddRehearsalCancellations`. `CancelAsync` vrací
      `Result<int>` jako ostatní `Create*`, ne entitu, a odmítne období bez
      naplánované zkoušky, obrácený rozsah, víc než 366 dní a důvod nad 200 znaků.
      GET smí každý přihlášený, POST a DELETE jen admin. `AttendanceReportService`
      zrušené zkoušky nepočítá. **Odchylka:** zrušená zkouška u `Upsert*Rehearsal`
      vrací 400, ne 409, podle konvence v `ResultHttpExtensions`: mobilní klienti
      ukážou text ze serveru jen u 400.)
- [x] **E. Admin UI v Blazoru**: seznam výjimek, formulář od–do + důvod,
      obnovení výjimky. Rychlá akce „Zrušit tuhle zkoušku“ přímo ze sloupce
      měsíční tabulky.
      (2026-10-01, uděláno až po F, aby UI rovnou volalo `RehearsalWorkflow`.
      Stránka `/Admin/Rehearsals` (jen admin, v menu „Zkoušky“) ukazuje nadcházející
      zkoušky na 8 týdnů a výjimky od začátku minulého měsíce. `CancelRehearsalDialog`
      má `MudForm` a zaškrtávátko „Upozornit členy“. V tabulce docházky má hlavička
      zkoušky ikonku zrušit nebo obnovit a zrušená zkouška ukazuje „odpadá“ s důvodem
      v tooltipu, bez buněk a s nulovými součty. Zrušení akce v `ListEvents` dostalo
      `CancelEventDialog` se stejným zaškrtávátkem. Založení, úprava, smazání
      a zrušení akce z Blazoru jedou přes `RehearsalWorkflow.EventChangedAsync`.
      **Opraveno navíc:** tabulka docházky párovala buňky jen podle data, takže
      zrušená akce zadaná jen datem (pátek 00:00) by se s vrácenou zkouškou
      o buňky přetahovala. Teď páruje podle `EventId` + data. E2E:
      `RehearsalCancellationTests`, `/Admin/Rehearsals` v `AdminWalkthroughTests`.)
- [x] **F. Důsledky zrušení**: úklid událostí v Google Calendar (stejná kompenzace
      jako u `ReconcileCalendarAfterFailedSaveAsync`). Volitelný push (`notify` v požadavku)
      pro zrušení zkoušky i akce, nový `NotificationType` pro obojí, aby šel dohledat
      v `SentNotifications`. Úklid Google
      událostí se týká i existující chyby „akce založená na pátek s vyplněnou
      zkouškou“ a měl by jet stejnou cestou.
      (2026-10-01: `Demizon.Mvc/Services/Rehearsal/RehearsalWorkflow.cs`. Všechny změny
      končí v `SyncCalendarAsync(days)`, které pro dotčené dny srovná Google události
      s tím, jestli je v ten den zkouška. Den bez zkoušky událost ztratí, den se
      zkouškou ji dostane u každého „ano“. Volá ho zrušení i obnovení výjimky
      a založení, úprava, smazání i zrušení akce, takže **obnovení** vrací události
      zpátky, i když to plán výslovně nežádal. `NotificationType.RehearsalCancelled`
      (13) a `EventCancelled` (5). Push při zrušení více zkoušek jde jednou na člena,
      ne za každý pátek. „Nemá ne“ u období znamená nemá „ne“ na všechny zrušené
      dny. API: `CancelRehearsalsRequest.Notify`, `PATCH /api/events/{id}/cancel?notify=true`.
      Test: `RehearsalWorkflowTests`.)
- [x] **G. Flutter**: zobrazit důvod zrušení, zakázat vyplnění. Admin akci zrušení
      v appce jen pokud bude chtít (Blazor admin stačí).
      (2026-10-01: `cancellationReason` v `Event` a `MonthlyColumn`. **Detail zkoušky
      si dřív `Event` skládal lokálně** s `isCancelled: false` (převzato z MAUI), takže
      by zrušenou zkoušku ukázal jako normální. Teď ho bere z `/api/events/month`
      (`findRehearsal`). Den, kdy podle serveru zkouška není, ukáže jako zamčený
      s „V tento den zkouška není.“ a lokální složení zůstává jen jako záloha bez
      spojení. Zamčená zkouška má místo formuláře kartu s důvodem, v seznamu „Odpadá“
      + důvod a v tabulce tooltip na hlavičce. Zrušení z appky se nedělá, admin má
      Blazor. MAUI se neměnil: `IsCancelled` už vykresluje a důvod nepotřebuje.
      Testy: skupina „zrušená zkouška“ v `test/contract_test.dart`. **Na telefonu
      neověřeno**, stejně jako zbytek Flutteru.)
- [x] **H. Dokumentace**: `notifications.md` (připomínky přeskakují zrušené zkoušky),
      `STATUS.md`, tady stav kroků. (2026-10-01, v `notifications.md` přibyly
      sekce 5 a 6 a nové `NotificationType`.)

### Doplněno 2026-10-29: zrušení z appky, vůdci

Podnět z prvního prokliku na telefonu: zkoušku je potřeba zrušit narychlo (málo
lidí) nebo dopředu (25. 12. 2026 je pátek).

- **Kdo:** POST a DELETE `/api/rehearsals/cancellations` i stránka
  `/Admin/Rehearsals` (a položka menu) jsou pro `RoleNames.RehearsalManagers`
  = `Admin` + `Leader`. Dřív jen admin. Testy v `RoleAuthorizationApiTests`
  (vůdce zruší i obnoví, správce krojárky dostane 403).
- **Flutter:** detail zkoušky má pro admina a vůdce kartu „Správa“
  s **Zrušit zkoušku** (dialog: důvod do 200 znaků, přepínač „Poslat notifikaci“,
  výchozí zapnutý) a u zrušené zkoušky **Obnovit zkoušku**. Ruší se jen ten jeden
  den; období (prázdniny) zůstává na webu.
- **Obnovení** potřebuje id výjimky, které `Event` nenese. Controller ji pro zrušenou
  zkoušku dohledá přes `GET /api/rehearsals/cancellations?from=den&to=den`. Den,
  kdy zkouška není kvůli akci, výjimku nemá, takže tlačítko Obnovit nedostane.
  Výjimka přes víc dní se obnoví celá a dialog to řekne s datem od–do.
- Stav: `EventDetailState.canCancelRehearsal` / `canRestoreRehearsal`, testy ve skupině
  „zrušená zkouška“ v `test/contract_test.dart`.

### Kde co je

| Co | Kde |
|---|---|
| Pravidlo (čistý výpočet) | `Demizon.Core/Services/Rehearsal/RehearsalSchedule.cs` |
| Načtení akcí a výjimek, správa výjimek | `Demizon.Core/Services/Rehearsal/RehearsalService.cs` |
| Nastavení (den, 19:00, 2 h 30) | sekce `Rehearsals` v `appsettings.json`, `RehearsalSettings` |
| Google Calendar a push při změnách | `Demizon.Mvc/Services/Rehearsal/RehearsalWorkflow.cs` |
| API výjimek | `Demizon.Mvc/Controllers/Api/RehearsalsController.cs` |
| Admin UI | `Pages/Admin/Attendance/Rehearsals.razor`, hlavička sloupce v `MemberAttendance.razor` |
| Testy | `RehearsalScheduleTests`, `RehearsalServiceTests`, `RehearsalApiTests`, `RehearsalWorkflowTests`, E2E `RehearsalCancellationTests` |

### Co se vědomě nedělalo

- **Google událost akce při zrušení akce** zůstává, jako dřív. Řeší se jen události
  *zkoušek*. Rušit i události akcí by chtělo vlastní rozhodnutí: zrušená akce se
  dá reaktivovat a událost by se pak musela znovu zakládat.
- **Validace mazání docházky na zkoušku** (`DELETE /api/attendances/rehearsal`)
  záměrně chybí, aby šla uklidit docházka na den, který zkouškou přestal být.
- **Report docházky** nepočítá zrušené zkoušky. Docházku na pátek, na který
  později připadla akce, ale počítá dál, jako dřív.
- **Blazor `ListEvents.RemoveEvent`** dál maže akci bez úklidu Google událostí
  *akce* (to dělá jen API). Mimo rozsah, jen se k tomu přidalo srovnání zkoušek.

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
