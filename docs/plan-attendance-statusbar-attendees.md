# Plán: Editace docházky, status bar, seznam účastníků na detailu akce

## Kontext

Aktuální stav má několik problémů:
1. **Admin editace docházky nefunguje** – MVC API kontroler nemá admin endpointy pro editaci docházky jiných členů (existují jen v Api projektu), takže MAUI app dostává 404 a zobrazuje "Nepodařilo se načíst docházku."
2. **Non-admin uživatelé** mohou kliknout na buňku docházky jiného člena u eventů (naviguje na broken stránku), u zkoušek vidí info zprávu – chování by mělo být konzistentní.
3. **Android status bar** je zatmavlý, protože nemá nastavenou barvu.
4. **Detail akce** nezobrazuje seznam účastníků ani sumy tanečníků/muzikantů.

---

## Feature 1: Oprava admin editace docházky + restrikce pro non-admin

### 1A: Přidat admin endpointy do MVC kontroleru

**Soubor:** `Demizon.Mvc/Controllers/Api/AttendancesController.cs`

Přidat 4 nové endpointy (zkopírovat logiku z `Demizon.Api/Controllers/AttendancesController.cs:104-190`):

- `[HttpGet("{eventId:int}/member/{memberId:int}")]` + `[Authorize(Roles = "Admin")]` – vrátí docházku člena pro event, nebo prázdné DTO pokud neexistuje
- `[HttpPut("{eventId:int}/member/{memberId:int}")]` + `[Authorize(Roles = "Admin")]` – upsert docházky člena pro event, včetně Google Calendar sync
- `[HttpGet("rehearsal/member/{memberId:int}")]` + `[Authorize(Roles = "Admin")]` + `[FromQuery] DateTime date` – vrátí docházku člena pro zkoušku
- `[HttpPut("rehearsal/member/{memberId:int}")]` + `[Authorize(Roles = "Admin")]` + `[FromQuery] DateTime date` – upsert docházky člena pro zkoušku (bez Google Calendar sync)

Potřeba přidat `IMemberService` a `IGoogleCalendarService` do constructor injection (stejně jako v Api kontroleru).

### 1B: Přidat Refit metody pro rehearsal admin endpointy

**Soubor:** `Demizon.Maui/Services/IApiClient.cs`

Přidat za řádek 46:
```csharp
[Get("/api/attendances/rehearsal/member/{memberId}")]
Task<AttendanceDto> GetMemberRehearsalAttendanceAsync(int memberId, [Query] DateTime date);

[Put("/api/attendances/rehearsal/member/{memberId}")]
Task<AttendanceDto> UpsertMemberRehearsalAttendanceAsync(int memberId, [Query] DateTime date, [Body] UpsertAttendanceRequest request);
```

(Event admin endpointy `GetMemberAttendanceAsync` a `UpsertMemberAttendanceAsync` už v IApiClient existují.)

### 1C: MemberAttendanceDetailViewModel – podpora zkoušek

**Soubor:** `Demizon.Maui/ViewModels/Attendance/MemberAttendanceDetailViewModel.cs`

- Přidat `[QueryProperty(nameof(RehearsalDateString), "rehearsalDate")]` a property `string? RehearsalDateString`
- Přidat computed `IsRehearsal` property: `EventId == 0 && !string.IsNullOrEmpty(RehearsalDateString)`
- Přidat computed `ShowRolePicker`: `IsAttending && !IsRehearsal` (zkoušky nemají roli)
- `LoadAsync()`: přidat `if (IsRehearsal)` větev – vytvořit syntetický `EventDto("Zkouška", ...)`, volat `GetMemberRehearsalAttendanceAsync`
- `SaveAsync()`: přidat `if (IsRehearsal)` větev – volat `UpsertMemberRehearsalAttendanceAsync` s `ActivityRole = null`

### 1D: MemberAttendanceDetailPage.xaml – skrýt role picker pro zkoušky

**Soubor:** `Demizon.Maui/Pages/Attendance/MemberAttendanceDetailPage.xaml`

- Změnit `IsVisible="{Binding IsAttending}"` na `IsVisible="{Binding ShowRolePicker}"` u Border s role pickerem (řádek 124-126)

### 1E: AllMembersAttendanceViewModel – přidat IsAdmin a navigaci pro zkoušky

**Soubor:** `Demizon.Maui/ViewModels/Attendance/AllMembersAttendanceViewModel.cs`

- Přidat `bool IsAdmin` property (cachovat v `LoadAsync()` stejně jako `_currentMemberId`)
- Přidat metodu `NavigateToMemberRehearsalAsync(DateTime date, int memberId, string memberName)` – naviguje na `MemberAttdDetail` s parametry `rehearsalDate`, `memberId`, `memberName`

### 1F: AllMembersAttendancePage.xaml.cs – admin gate na buňkách

**Soubor:** `Demizon.Maui/Pages/Attendance/AllMembersAttendancePage.xaml.cs` (řádky 263-302)

- **Event buňky jiných členů** (řádek 276): přidat podmínku `if (_vm!.IsAdmin)` → navigovat, `else` → zobrazit info "Editace docházky jiných členů je dostupná pouze pro administrátory."
- **Rehearsal buňky jiných členů** (řádek 296): `if (_vm!.IsAdmin)` → `NavigateToMemberRehearsalAsync(...)`, `else` → stejná info zpráva

---

## Feature 2: Android status bar

### 2A: Nastavit barvu status baru

**Soubor:** `Demizon.Maui/Platforms/Android/MainActivity.cs`

V `OnCreate()` po `base.OnCreate(savedInstanceState)` přidat:
```csharp
if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
{
    Window!.SetStatusBarColor(Android.Graphics.Color.ParseColor("#9A7450"));
}
```

Barva `#9A7450` = Accent, shodná se Shell.BackgroundColor (navigační lišta). Ikony zůstanou bílé (výchozí pro tmavší barvu pozadí).

---

## Feature 3: Seznam účastníků na detailu akce

### 3A: Vytvořit DTO

**Nový soubor:** `Demizon.Contracts/Events/EventAttendeesDto.cs`

```csharp
namespace Demizon.Contracts.Events;

public sealed record EventAttendeeDto(int MemberId, string FullName, string? ActivityRole);

public sealed record EventAttendeesDto(
    List<EventAttendeeDto> Attendees,
    int DancerCount,
    int MusicianCount,
    int TotalCount);
```

### 3B: Přidat API endpoint

**Soubor:** `Demizon.Mvc/Controllers/Api/EventsController.cs`

Přidat za `GetOne` (po řádku 104):
```csharp
[HttpGet("{id:int}/attendees")]
public async Task<ActionResult<EventAttendeesDto>> GetAttendees(int id)
```

- Query: `attendanceService.GetAll().Where(a => a.EventId == id && a.Status == AttendanceStatus.Yes).Include(a => a.Member)`
- Mapovat na `EventAttendeeDto`, spočítat dancer/musician/total

Potřeba přidat `using Demizon.Dal.Entities;` (pro `AttendanceStatus`).

### 3C: Přidat Refit metodu

**Soubor:** `Demizon.Maui/Services/IApiClient.cs`

```csharp
[Get("/api/events/{id}/attendees")]
Task<EventAttendeesDto> GetEventAttendeesAsync(int id);
```

### 3D: Rozšířit EventDetailViewModel

**Soubor:** `Demizon.Maui/ViewModels/EventDetailViewModel.cs`

- Přidat `[ObservableProperty] EventAttendeesDto? _attendees`
- Přidat computed `ShowAttendees`: `!IsRehearsal && Attendees?.TotalCount > 0`
- V `LoadAsync()` (else větev pro eventy): po načtení eventu zavolat `apiClient.GetEventAttendeesAsync(EventId)` v try/catch

### 3E: Rozšířit EventDetailPage.xaml

**Soubor:** `Demizon.Maui/Pages/EventDetailPage.xaml`

Přidat za hero card (řádek 44), před "Moje docházka" sekci, novou sekci "Kdo přijde":
- Border card s `IsVisible="{Binding ShowAttendees}"`
- Nadpis "Kdo přijde"
- Sumy: Celkem / Tanečníci / Muzikanti v `HorizontalStackLayout`
- Seznam účastníků přes `BindableLayout` na `StackLayout` (ne `CollectionView` – vyhnout se nested scrolling problémům)
- Každý účastník: zelená fajfka + jméno + role

Přidat XML namespace: `xmlns:events="clr-namespace:Demizon.Contracts.Events;assembly=Demizon.Contracts"`

---

## Pořadí implementace

1. Feature 2 (status bar) – nejmenší, nezávislá
2. Feature 1A-1B (MVC endpointy + Refit) – backend
3. Feature 1C-1D (ViewModel + XAML pro rehearsal support)
4. Feature 1E-1F (admin gate v přehledu)
5. Feature 3A-3B (DTO + API endpoint)
6. Feature 3C-3E (MAUI attendees display)

## Ověření

- **Feature 1**: Přihlásit se jako admin → přehled docházky → kliknout na buňku jiného člena u eventu i zkoušky → ověřit, že se načte záznam, lze editovat a uložit. Přihlásit se jako non-admin → klik na buňku jiného člena → info zpráva.
- **Feature 2**: Spustit app na Android emulátoru/zařízení → status bar má barvu `#9A7450`, není zatmavlý.
- **Feature 3**: Otevřít detail akce → pod hero card vidět sekci "Kdo přijde" se seznamem a sumami.
