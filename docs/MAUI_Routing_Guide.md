# Demizon MAUI — Routing & Navigation Guide

**Datum**: 2026-04-16
**Stav**: Best-practice referenční dokument (v2 — opravený routing pattern)
**Účel**: Definuje pravidla a vzory navigace v MAUI aplikaci.

---

## 1. Klíčové pravidlo: PLOCHÉ názvy rout (bez lomítek)

### Proč nesmí registrovaná routa obsahovat `/`

MAUI Shell při navigaci parsuje cestu po segmentech oddělenýchych `/`. První segment porovnává s vizuálním stromem Shellu (taby, flyout items). Pokud se shoduje s existujícím tab-route, Shell se pokusí o **hierarchickou navigaci na shell element** — ta selže s chybou:

```
Relative routing to shell elements is currently not supported.
Try prefixing your uri with ///
```

**Příklad problému:**
```
AppShell.xaml:  <ShellContent Route="events" .../>     ← tab-route "events"
AppRoutes.cs:   EventDetail = "events/detail"           ← registrovaná routa

→ Shell parsuje "events/detail", najde tab "events" → CRASH
```

**Správný vzor:**
```csharp
// ŠPATNĚ — obsahuje "/" → kolize s tab-route
public const string EventDetail = "events/detail";   // ❌
public const string DanceDetail = "dances/detail";   // ❌

// SPRÁVNĚ — ploché, jednoslovné identifikátory oddělené pomlčkou
public const string EventDetail = "event-detail";    // ✅
public const string DanceDetail = "dance-detail";    // ✅
public const string AttdStats   = "attd-stats";      // ✅
```

**Pravidlo**: Registrované routy (`Routing.RegisterRoute`) jsou **ploché identifikátory**, ne URL cesty. Nikdy nepoužívejte `/` v názvu routy.

---

## 2. Absolutní vs relativní routy

| Scénář                              | Typ routy       | Příklad                          |
|-------------------------------------|-----------------|----------------------------------|
| Login → hlavní aplikace             | **Absolutní** (`//`) | `//main/attendance`         |
| Logout → login                      | **Absolutní** (`//`) | `//login`                   |
| AuthHandler 401 → vynucený logout   | **Absolutní** (`//`) | `//login`                   |
| Tab → detail (push na zásobník)     | **Relativní**        | `event-detail?eventId=5`    |
| Detail → zpět                       | **Relativní**        | `..`                        |
| Tab → sub-stránka (stats, overview) | **Relativní**        | `attd-stats`                |

**Zlaté pravidlo:** Absolutní routu (`//`) používejte **pouze** pro přepnutí autentizačního kontextu (login ↔ taby). Vše ostatní je relativní push.

---

## 3. Centrální routovací konstanty (`AppRoutes.cs`)

Jediný zdroj pravdy pro všechny routovací řetězce. Žádné magické stringy ve ViewModelech.

```csharp
public static class AppRoutes
{
    // ── Absolutní (autentizační přechody) ──────────────────────
    public const string Login    = "//login";
    public const string MainTabs = "//main/attendance";

    // ── Detail / push stránky ─────────────────────────────────
    // DŮLEŽITÉ: Vždy ploché, bez "/" — viz sekce 1
    public const string EventDetail    = "event-detail";
    public const string EventCreate    = "event-create";
    public const string DanceDetail    = "dance-detail";
    public const string AttdStats      = "attd-stats";
    public const string AttdOverview   = "attd-overview";
    public const string MemberAttdDetail = "member-attd-detail";
}
```

---

## 4. Registrace rout (`AppShell.xaml.cs`)

```csharp
public AppShell()
{
    InitializeComponent();

    // Registrujte pouze stránky, které NEJSOU přímou součástí TabBaru.
    Routing.RegisterRoute(AppRoutes.EventDetail,      typeof(Pages.EventDetailPage));
    Routing.RegisterRoute(AppRoutes.EventCreate,      typeof(Pages.CreateEventPage));
    Routing.RegisterRoute(AppRoutes.DanceDetail,      typeof(Pages.DanceDetailPage));
    Routing.RegisterRoute(AppRoutes.AttdStats,        typeof(Pages.Attendance.AttendanceStatsPage));
    Routing.RegisterRoute(AppRoutes.AttdOverview,     typeof(Pages.Attendance.AllMembersAttendancePage));
    Routing.RegisterRoute(AppRoutes.MemberAttdDetail, typeof(Pages.Attendance.MemberAttendanceDetailPage));
}
```

---

## 5. INavigationService — abstrakce navigace

ViewModely nikdy nevolají `Shell.Current.GoToAsync()` přímo. Místo toho injectují `INavigationService`:

```csharp
public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}
```

**Proč**: Testovatelnost (mock navigace v unit testech), centralizované logování, snadný refactoring.

---

## 6. Navigační mapa

```
LoginPage  ←──── //login
    │
    │  //main/attendance
    ▼
┌─TabBar──────────────────────────────────────────┐
│  [Docházka]     [Akce]      [Tance]    [Profil] │
│  attendance     events      dances     profile   │
│   │  │  │         │  │         │          │      │
│   │  │  └─────────┼──┼─────────┘          │      │
│   │  │   event-   │  │  dance-        //login    │
│   │  │   detail   │  │  detail       (logout)    │
│   │  │            │  └─ event-create             │
│   │  └─ attd-stats                               │
│   └── attd-overview                              │
│         └─ member-attd-detail                    │
└──────────────────────────────────────────────────┘

//…  = absolutní routa (auth context switch)
…    = relativní routa (push na zásobník aktuálního tabu)
..   = zpět (pop)
```

---

## 7. Přidání nové stránky — checklist

1. Vytvořte `Pages/NewPage.xaml` + `.xaml.cs` a `ViewModels/NewViewModel.cs`
2. Přidejte konstantu do `AppRoutes.cs` — **plochý** název, bez `/`
3. Zaregistrujte v `AppShell.xaml.cs`: `Routing.RegisterRoute(AppRoutes.New, typeof(...))`
4. Zaregistrujte v DI (`MauiProgram.cs`): `services.AddTransient<NewViewModel>(); services.AddTransient<NewPage>();`
5. Navigujte relativně: `await _navigation.GoToAsync($"{AppRoutes.New}?id={item.Id}")`

---

## 8. Životní cyklus stránky

### OnAppearing → LoadCommand

```csharp
protected override void OnAppearing()
{
    base.OnAppearing();
    if (BindingContext is MyViewModel vm)
        vm.LoadCommand.Execute(null);
}
```

`QueryProperty` hodnoty jsou nastavené **před** `OnAppearing`, takže `LoadAsync()` může bezpečně číst parametry.

### Messenger pro cross-tab refresh

```csharp
// Po uložení změny, která ovlivňuje data na jiném tabu:
WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
```

---

## 9. Autentizační flow

```
App Start → TokenStorage.InitializeAsync()
  ├─ token platný      → GoToAsync("//main/attendance")
  ├─ refresh existuje  → TokenRefreshHelper.RefreshAsync()
  │     ├─ úspěch      → SaveAsync() → GoToAsync("//main/attendance")
  │     └─ selhání     → Clear() → zůstane na LoginPage
  └─ nic               → zůstane na LoginPage

Během používání (AuthHandler):
  ├─ token brzy expiruje → TryRefreshAsync() → pokračuj
  └─ API vrátí 401       → TryRefreshAsync()
        ├─ úspěch → opakuj request
        └─ selhání → Clear() + GoToAsync("//login")
```

---

## 10. Code review checklist

- [ ] Routa je z `AppRoutes` konstanty (žádné magické stringy)
- [ ] Název routy je **plochý** — neobsahuje `/`
- [ ] Detail/sub-stránka používá **relativní** routu (bez `//`)
- [ ] Absolutní routa (`//`) se používá **pouze** pro login/logout
- [ ] Nová stránka je registrovaná v `AppShell.xaml.cs` i v `MauiProgram.cs`
- [ ] ViewModel přijímá `INavigationService` přes konstruktor
- [ ] QueryProperty parametry se zpracovávají v `LoadAsync()`, ne v setteru
- [ ] Navigace zpět je `await _navigation.GoBackAsync()`, ne hardcoded `".."`
