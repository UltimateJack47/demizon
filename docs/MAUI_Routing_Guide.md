# Demizon MAUI — Routing & Navigation Guide

**Datum**: 2026-04-15  
**Stav**: Best-practice referenční dokument  
**Účel**: Definuje pravidla a vzory navigace v MAUI aplikaci, aby opravy v jedné části nerozbíjely druhou.

---

## 1. Nalezené problémy (audit stávajícího stavu)

### 🔴 P1 — Míchání absolutních a relativních rout

**Nejzávažnější problém.** Tentýž cíl (`events/detail`) je volán dvěma různými způsoby:

| Zdroj                             | Volání                                               | Typ      |
|-----------------------------------|------------------------------------------------------|----------|
| `EventsViewModel`                 | `//events/detail?eventId=…`                          | Absolutní |
| `AttendanceViewModel`             | `events/detail?eventId=…`                            | Relativní |
| `AllMembersAttendanceViewModel`   | `events/detail?eventId=…`                            | Relativní |
| `DancesViewModel`                 | `//dances/detail?danceId=…`                          | Absolutní |

**Proč je to problém:**

- `//events/detail` = navigace přes kořen Shellu → přepne aktivní tab na **Akce** a pak push detail.
- `events/detail` = relativní push na aktuální zásobník → uživatel zůstane na **Docházce**.

Výsledek: detail stejné akce vypadá jinak podle toho, odkud přicházíte — jiná záložka je zvýrazněná, návrat (`..`) vede na jiný seznam. **Oprava absolutní routy rozbije relativní a naopak.**

### 🔴 P2 — Nesouhlasná base URL v AuthHandler

| Soubor           | Android                      | Jiné              |
|------------------|------------------------------|--------------------|
| `ApiConfig.cs`   | `http://192.168.1.100:5272`  | `http://localhost:5272` |
| `AuthHandler.cs` | `http://10.0.2.2:5000`      | `http://localhost:5000` |

`AuthHandler.GetBaseAddress()` používá **jiný port (5000)** i **jinou adresu** než zbytek aplikace. Token refresh v `AuthHandler` tedy míří na neexistující server → selže → uživatel je odhlášen → aplikace jde na login.

### 🟡 P3 — Duplicitní logika refreshe tokenu

`App.xaml.cs` i `AuthHandler.cs` mají každý vlastní `HttpClient` + vlastní JSON serializaci pro `/api/auth/refresh`. Dvě kopie znamenají dvě místa pro bugy a dvě různé base URL (viz P2).

### 🟡 P4 — Magické stringy rout rozptýlené po ViewModelech

Routy jako `"//attendance"`, `"events/detail"`, `"attd-stats"` jsou hardcodované na **15 místech** v 8 souborech. Přejmenování routy vyžaduje grep + ruční nahrazení.

### 🟡 P5 — Žádná abstrakce navigace

Každý ViewModel volá přímo `Shell.Current.GoToAsync(…)`. To ztěžuje:
- **testování** (nelze mockovat navigaci v unit testech),
- **refactoring** (změna navigační strategie = přepsání všech ViewModelů),
- **sledování** (logování navigačních přechodů).

### 🟢 P6 — Transient registrace stránek a ViewModelů

Všechny Pages i ViewModels jsou registrované jako `Transient`, což je **správně** — Shell vytvoří čistou instanci při každé navigaci, takže nehrozí leakování starého stavu z QueryProperties.

---

## 2. Navrhovaný best-practice pattern

### 2.1 Centrální routovací konstanty (`AppRoutes`)

Jediný zdroj pravdy pro všechny routovací řetězce.

```csharp
// Demizon.Maui/AppRoutes.cs
namespace Demizon.Maui;

/// <summary>
/// Jediný zdroj pravdy pro všechny Shell routy.
/// Absolutní routy (//…) se používají POUZE pro přepnutí autentizačního kontextu
/// (login ↔ main). Vše ostatní je relativní push na aktuální navigační zásobník.
/// </summary>
public static class AppRoutes
{
    // ── Absolutní (autentizační přechody) ──────────────────────
    public const string Login      = "//login";
    public const string MainTabs   = "//main/attendance";   // výchozí tab po přihlášení

    // ── Tabs (pro přímou absolutní navigaci na konkrétní tab) ─
    public const string Attendance = "//main/attendance";
    public const string Events     = "//main/events";
    public const string Dances     = "//main/dances";
    public const string Profile    = "//main/profile";

    // ── Detail / push stránky (VŽDY relativní) ────────────────
    public const string EventDetail   = "events/detail";
    public const string EventCreate   = "events/create";
    public const string DanceDetail   = "dances/detail";
    public const string AttdStats     = "attd-stats";
    public const string AttdOverview  = "attd-overview";
}
```

### 2.2 Pravidlo: Absolutní vs relativní routy

| Scénář                                   | Typ routy   | Příklad                               |
|------------------------------------------|-------------|---------------------------------------|
| Login → hlavní aplikace                  | **Absolutní** (`//`) | `//main/attendance`            |
| Logout → login                           | **Absolutní** (`//`) | `//login`                      |
| AuthHandler 401 → vynucený logout        | **Absolutní** (`//`) | `//login`                      |
| Tab → detail (push na zásobník)          | **Relativní**        | `events/detail?eventId=5`      |
| Detail → zpět                            | **Relativní**        | `..`                           |
| Tab → sub-stránka (stats, overview)      | **Relativní**        | `attd-stats`                   |

**Zlaté pravidlo:** absolutní routu (`//`) používejte **jen** při přepnutí autentizačního kontextu (login ↔ taby). Vše ostatní je relativní push.

### 2.3 Registrace rout (AppShell.xaml.cs)

```csharp
public AppShell()
{
    InitializeComponent();

    // Registrujte pouze stránky, které NEJSOU přímou součástí Shellu/TabBaru.
    // Používejte konstanty z AppRoutes.
    Routing.RegisterRoute(AppRoutes.EventDetail,  typeof(Pages.EventDetailPage));
    Routing.RegisterRoute(AppRoutes.EventCreate,   typeof(Pages.CreateEventPage));
    Routing.RegisterRoute(AppRoutes.DanceDetail,   typeof(Pages.DanceDetailPage));
    Routing.RegisterRoute(AppRoutes.AttdStats,     typeof(Pages.Attendance.AttendanceStatsPage));
    Routing.RegisterRoute(AppRoutes.AttdOverview,  typeof(Pages.Attendance.AllMembersAttendancePage));
}
```

### 2.4 Navigace ve ViewModelech — INavigationService

Abstrakce navigace umožňuje testování i centralizované logování.

```csharp
// Demizon.Maui/Services/INavigationService.cs
namespace Demizon.Maui.Services;

public interface INavigationService
{
    Task GoToAsync(string route);
    Task GoToAsync(string route, IDictionary<string, object> parameters);
    Task GoBackAsync();
}
```

```csharp
// Demizon.Maui/Services/ShellNavigationService.cs
namespace Demizon.Maui.Services;

public class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route)
        => Shell.Current.GoToAsync(route);

    public Task GoToAsync(string route, IDictionary<string, object> parameters)
        => Shell.Current.GoToAsync(route, parameters);

    public Task GoBackAsync()
        => Shell.Current.GoToAsync("..");
}
```

Registrace v DI:
```csharp
services.AddSingleton<INavigationService, ShellNavigationService>();
```

### 2.5 Příklad navigace ve ViewModelu

**Před (špatně):**
```csharp
await Shell.Current.GoToAsync($"//events/detail?eventId={eventDto.Id}");
```

**Po (správně):**
```csharp
await _navigation.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventDto.Id}");
```

### 2.6 Jednotná base URL

```csharp
// Demizon.Maui/ApiConfig.cs — jediný zdroj pravdy pro URL
internal static class ApiConfig
{
#if ANDROID
    public const string BaseUrl = "http://192.168.1.100:5272";
#else
    public const string BaseUrl = "http://localhost:5272";
#endif
}
```

`AuthHandler.GetBaseAddress()` musí být odstraněna a nahrazena:
```csharp
refreshClient.BaseAddress = new Uri(ApiConfig.BaseUrl);
```

### 2.7 Jednotný token refresh

Odstraňte `RefreshTokenDirectAsync()` z `App.xaml.cs`. Místo toho:

```csharp
// App.xaml.cs — TryAutoLoginAsync()
// Použijte stejný IApiClient (Refit) i pro refresh,
// nebo extrahujte sdílenou helper třídu:
public static class TokenRefreshHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<TokenResponse?> RefreshAsync(string refreshToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(ApiConfig.BaseUrl) };
        var payload = JsonSerializer.Serialize(new RefreshRequest(refreshToken), JsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/refresh", content);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);
    }
}
```

Volání z `App.xaml.cs` i `AuthHandler.cs` pak projde přes `TokenRefreshHelper.RefreshAsync(…)`.

---

## 3. Navigační mapa aplikace

```
┌─────────────────────────────────────────────────────────────┐
│  LoginPage  ←──── //login (absolutní, auth context switch)  │
│      │                                                      │
│      │  //main/attendance (absolutní, auth context switch)   │
│      ▼                                                      │
│  ┌─TabBar─────────────────────────────────────────────────┐ │
│  │                                                         │ │
│  │  [Docházka]      [Akce]       [Tance]      [Profil]   │ │
│  │      │              │            │              │       │ │
│  │      │              │            │              │       │ │
│  │  AttendancePage  EventsPage  DancesPage   ProfilePage  │ │
│  │   │  │  │          │  │          │              │       │ │
│  │   │  │  │          │  │          │          //login     │ │
│  │   │  │  │          │  │          │        (logout)      │ │
│  │   │  │  │          │  │          │                      │ │
│  │   │  │  └──────────┼──┼──────────┘                      │ │
│  │   │  │     events/ │  │  dances/                        │ │
│  │   │  │     detail  │  │  detail                         │ │
│  │   │  │        │    │  │    │                             │ │
│  │   │  │        ▼    │  │    ▼                             │ │
│  │   │  │  EventDetail│  │ DanceDetail                     │ │
│  │   │  │     Page    │  │    Page                          │ │
│  │   │  │             │  │                                  │ │
│  │   │  │             │  └─ events/create                   │ │
│  │   │  │             │         │                           │ │
│  │   │  │             │         ▼                           │ │
│  │   │  │             │   CreateEventPage                   │ │
│  │   │  │                                                   │ │
│  │   │  └─ attd-stats ──► AttendanceStatsPage               │ │
│  │   └── attd-overview ─► AllMembersAttendancePage          │ │
│  │                                                         │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘

Legenda:
  //…  = absolutní routa (auth context switch)
  …    = relativní routa (push na zásobník aktuálního tabu)
  ..   = zpět (pop)
```

---

## 4. Pravidla pro přidání nové stránky

### Krok 1: Vytvořte Page + ViewModel

```
Pages/NewFeaturePage.xaml        + .xaml.cs
ViewModels/NewFeatureViewModel.cs
```

### Krok 2: Přidejte routovací konstantu

```csharp
// AppRoutes.cs
public const string NewFeature = "new-feature";
```

### Krok 3: Registrujte routu

```csharp
// AppShell.xaml.cs → konstruktor
Routing.RegisterRoute(AppRoutes.NewFeature, typeof(Pages.NewFeaturePage));
```

### Krok 4: Zaregistrujte v DI

```csharp
// MauiProgram.cs
services.AddTransient<NewFeatureViewModel>();
services.AddTransient<NewFeaturePage>();
```

### Krok 5: Navigujte relativně

```csharp
// Z jakéhokoliv ViewModelu
await _navigation.GoToAsync($"{AppRoutes.NewFeature}?id={item.Id}");
```

### Krok 6: Přijímejte parametry

```csharp
[QueryProperty(nameof(ItemId), "id")]
public partial class NewFeatureViewModel : ObservableObject
{
    [ObservableProperty]
    private int _itemId;

    [RelayCommand]
    public async Task LoadAsync()
    {
        // ItemId je již nastavený z QueryProperty
        var data = await _apiClient.GetItemAsync(ItemId);
    }
}
```

---

## 5. Životní cyklus stránky a načítání dat

### Pattern: OnAppearing → LoadCommand

```csharp
// Page code-behind
public partial class MyPage : ContentPage
{
    public MyPage(MyViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MyViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
```

**Proč:**
- `OnAppearing` se volá pokaždé, když je stránka viditelná (i po návratu z detailu).
- Data se tak vždy refreshnou.
- `QueryProperty` hodnoty jsou nastavené **před** `OnAppearing`.

### Pattern: Messenger pro cross-tab refresh

```csharp
// Po uložení změny, která ovlivňuje data na jiném tabu:
WeakReferenceMessenger.Default.Send(new EventsChangedMessage());

// ViewModel, který chce reagovat:
public class EventsViewModel : ObservableObject, IRecipient<EventsChangedMessage>
{
    public EventsViewModel(IApiClient apiClient)
    {
        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(EventsChangedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() => LoadCommand.Execute(null));
    }
}
```

---

## 6. Autentizační flow

```
  App Start
      │
      ▼
  TokenStorage.InitializeAsync()
      │
      ├─ token platný? ──────────► GoToAsync("//main/attendance")
      │
      ├─ refresh token existuje?
      │       │
      │       ▼
      │  TokenRefreshHelper.RefreshAsync()
      │       │
      │       ├─ úspěch ─► SaveAsync() → GoToAsync("//main/attendance")
      │       │
      │       └─ selhání ─► Clear() → zůstane na LoginPage
      │
      └─ nic ─────────────► zůstane na LoginPage


  Během používání (AuthHandler):
      │
      ├─ API volání → token brzy expiruje?
      │       └─ TryRefreshAsync() → pokračuj
      │
      └─ API vrátí 401?
              └─ TryRefreshAsync()
                      ├─ úspěch → opakuj request
                      └─ selhání → Clear() + GoToAsync("//login")
```

---

## 7. Checklist pro code review navigace

- [ ] Routovací řetězec je z `AppRoutes` konstanty (žádné magické stringy)
- [ ] Detail/sub-stránka používá **relativní** routu (bez `//`)
- [ ] Absolutní routa (`//`) se používá **pouze** pro login/logout přechod
- [ ] Nová stránka je registrovaná v `AppShell.xaml.cs` i v `MauiProgram.cs`
- [ ] ViewModel přijímá `INavigationService` přes konstruktor
- [ ] QueryProperty parametry se zpracovávají v `LoadAsync()`, ne v setteru
- [ ] Navigace zpět je `await _navigation.GoBackAsync()`, ne hardcoded `".."`
- [ ] Base URL je z `ApiConfig.BaseUrl` (žádná duplikátní URL)

---

## 8. Soubory k úpravě (implementační plán)

| #  | Soubor                          | Změna                                              |
|----|----------------------------------|----------------------------------------------------|
| 1  | `AppRoutes.cs` (**nový**)        | Centrální routovací konstanty                      |
| 2  | `Services/INavigationService.cs` (**nový**) | Rozhraní navigační služby             |
| 3  | `Services/ShellNavigationService.cs` (**nový**) | Shell implementace                 |
| 4  | `Services/TokenRefreshHelper.cs` (**nový**) | Sdílený token refresh               |
| 5  | `AppShell.xaml.cs`               | Použít `AppRoutes` konstanty                       |
| 6  | `App.xaml.cs`                    | Použít `AppRoutes` + `TokenRefreshHelper`          |
| 7  | `Services/AuthHandler.cs`        | Smazat `GetBaseAddress()`, použít `ApiConfig` + `TokenRefreshHelper` |
| 8  | `MauiProgram.cs`                 | Registrovat `INavigationService`                   |
| 9  | `ViewModels/LoginViewModel.cs`   | `INavigationService` + `AppRoutes`                 |
| 10 | `ViewModels/EventsViewModel.cs`  | Relativní routy + `INavigationService` + `AppRoutes` |
| 11 | `ViewModels/EventDetailViewModel.cs` | `INavigationService` + `AppRoutes`             |
| 12 | `ViewModels/CreateEventViewModel.cs` | `INavigationService` + `AppRoutes`             |
| 13 | `ViewModels/DancesViewModel.cs`  | Relativní routy + `INavigationService` + `AppRoutes` |
| 14 | `ViewModels/DanceDetailViewModel.cs` | `INavigationService` + `AppRoutes`             |
| 15 | `ViewModels/ProfileViewModel.cs` | `INavigationService` + `AppRoutes`                 |
| 16 | `ViewModels/Attendance/AttendanceViewModel.cs` | `INavigationService` + `AppRoutes`   |
| 17 | `ViewModels/Attendance/AllMembersAttendanceViewModel.cs` | `INavigationService` + `AppRoutes` |

---

## 9. Souhrn klíčových pravidel

1. **Routy jsou konstanty** — vždy z `AppRoutes`, nikdy stringový literál.
2. **Absolutní = jen auth** — `//login` a `//main/attendance`, nic jiného.
3. **Relativní = vše ostatní** — detail, sub-stránky, modály.
4. **Navigace přes službu** — `INavigationService`, ne přímý `Shell.Current`.
5. **Jedna base URL** — `ApiConfig.BaseUrl` je jediný zdroj.
6. **Jeden refresh helper** — `TokenRefreshHelper` sdílený mezi `App` a `AuthHandler`.
7. **OnAppearing = load** — data se vždy refreshnou při zobrazení stránky.
