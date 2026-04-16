# Plan: "Nevím" (Maybe) Attendance Status Feature

## Context
Members currently can only mark attendance as Yes or No. The request is to add a third "Maybe/Nevím" state that:
- Visually signals "I saw the event but haven't decided yet"
- Continues sending reminder notifications (unlike Yes/No which suppresses them)
- Works in both MVC (Blazor) and MAUI apps

## Key Design Decision
`AttendanceStatus` enum: `No=0, Yes=1, Maybe=2`. SQLite stores enums as integers — existing `Attends=false(0)` → `Status=No(0)`, `Attends=true(1)` → `Status=Yes(1)`. **Zero data loss** — pure column rename migration. The contracts layer uses lowercase strings (`"yes"`, `"no"`, `"maybe"`) to stay JSON-friendly and avoid C# enum serialization issues across clients.

---

## Implementation Order

### 1. DAL — Entity + Migration

**`Demizon.Dal/Entities/Attendance.cs`**
- Add `AttendanceStatus` enum (No=0, Yes=1, Maybe=2) in same file
- Replace `bool Attends { get; set; } = false;` with `AttendanceStatus Status { get; set; } = AttendanceStatus.No;`

**`Demizon.Dal/DemizonContext.cs`** (line 128)
- Change `b.Property(s => s.Attends).HasDefaultValue(false)` → `b.Property(s => s.Status).HasDefaultValue(AttendanceStatus.No).HasConversion<int>()`

**Create migration** `20260416_AttendanceStatusMaybe.cs` (rename column):
```csharp
migrationBuilder.RenameColumn(name: "Attends", table: "Attendances", newName: "Status");
```

**`Demizon.Dal/Migrations/DemizonContextModelSnapshot.cs`** (lines 33-36)
- Change `b.Property<bool>("Attends")` → `b.Property<int>("Status")`, default 0

---

### 2. Contracts — DTO Changes

**`Demizon.Contracts/Attendances/AttendanceDto.cs`**
```csharp
public sealed record AttendanceDto(int Id, string Status, string? Comment, string? ActivityRole, DateTime LastUpdated);
```

**`Demizon.Contracts/Attendances/UpsertAttendanceRequest.cs`**
```csharp
public sealed record UpsertAttendanceRequest(string Status, string? Comment, string? ActivityRole);
```

**`Demizon.Contracts/Attendances/MemberCellDto.cs`**
```csharp
public sealed record MemberCellDto(DateTime Date, int? EventId, string? Status);
```

---

### 3. API — Mapping + Controllers

**`Demizon.Api/Mapping/ContractMappingExtensions.cs`** (line 15)
- `new AttendanceDto(a.Id, a.Attends, ...)` → `new AttendanceDto(a.Id, a.Status.ToString().ToLowerInvariant(), ...)`

**`Demizon.Api/Controllers/AttendancesController.cs`**
- Add `ParseStatus` helper: `"yes"→Yes, "maybe"→Maybe, _→No`
- Replace `attendance.Attends = request.Attends` with `attendance.Status = ParseStatus(request.Status)` (both `Upsert` and `UpsertMemberAttendance` endpoints)
- GCal logic: only create event for `Yes`; delete GCal event when switching to `No` **or** `Maybe`
- Fix GetMemberAttendance blank DTO: `new AttendanceDto(0, "no", null, null, DateTime.MinValue)`
- Fix table endpoint: `att?.Attends` → `att?.Status.ToString().ToLowerInvariant()`

---

### 4. Core Services

**`Demizon.Core/Services/Attendance/AttendanceReportService.cs`** (lines 24, 32-33)
- `a.Attends` → `a.Status` in Select
- `x.Attends && x.EventId == null` → `x.Status == AttendanceStatus.Yes && x.EventId == null`
- `x.Attends && x.EventId != null` → `x.Status == AttendanceStatus.Yes && x.EventId != null`

**`Demizon.Mvc/Services/AttendanceReminderBackgroundService.cs`** (lines 48-56)
- Change: currently notifies members with **no record**
- New: also notify members whose record has `Status == AttendanceStatus.Maybe`
```csharp
// Members with no record OR with Maybe status should still receive reminders
var decidedMemberIds = await db.Attendances
    .Where(a => a.EventId == ev.Id && a.Status != AttendanceStatus.Maybe)
    .Select(a => a.MemberId)
    .ToListAsync(ct);
var membersToNotify = await db.Members
    .Where(m => !decidedMemberIds.Contains(m.Id))
    .Select(m => new { m.Id })
    .ToListAsync(ct);
```

---

### 5. MVC — ViewModels + Razor

**`Demizon.Mvc/ViewModels/AttendanceViewModel.cs`**
- `bool Attends { get; set; }` → `AttendanceStatus Status { get; set; } = AttendanceStatus.No;`
- `ToViewModel`: `Attends = entity.Attends` → `Status = entity.Status`
- `ToEntity`: `Attends = vm.Attends` → `Status = vm.Status`

**`Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor`**
- Replace `day.Attends ? Icons.Material.Filled.Check : Icons.Material.Filled.Close` with 3-state:
  - `Yes` → `Check` (Color.Success)
  - `Maybe` → `Help` (Color.Warning)
  - `No` → `Close` (Color.Error)
- Same for `extDay` (externals section)

**`Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor.cs`**
- `CountAttending`: change `y.Attends` → `y.Status == AttendanceStatus.Yes`
- Same for `CountAttendingFemaleDancers`, `CountAttendingMaleDancers`, `CountAttendingMusicians`
- `attendsAfterSave = attendanceResult.Attends` → `attendsAfterSave = attendanceResult.Status == AttendanceStatus.Yes`
- `GetThemeStyle`: 3-way switch (Yes=success, Maybe=warning, No=error)
- `SyncGoogleCalendarAsync(model, bool attends, ...)` → `SyncGoogleCalendarAsync(model, AttendanceStatus status, ...)`; only create GCal for `Yes`, delete for `No` or `Maybe`
- `LoadData`: `attendance.Attends = userAttendance.Attends` → `attendance.Status = userAttendance.Status`

**`Demizon.Mvc/Pages/Admin/Attendance/Components/AttendanceForm.razor`**
- Replace `<MudCheckBox @bind-Value="Model.Attends">` with:
```razor
<MudRadioGroup T="AttendanceStatus" @bind-Value="Model.Status">
    <MudRadio Value="AttendanceStatus.Yes" Color="Color.Success">Zúčastním se</MudRadio>
    <MudRadio Value="AttendanceStatus.Maybe" Color="Color.Warning">Nevím</MudRadio>
    <MudRadio Value="AttendanceStatus.No" Color="Color.Error">Nezúčastním se</MudRadio>
</MudRadioGroup>
```
- Role picker: change visibility from `Model.Attends` to `Model.Status == AttendanceStatus.Yes`

---

### 6. MAUI — ViewModels

**`Demizon.Maui/ViewModels/EventDetailViewModel.cs`**
- Replace `[ObservableProperty] private bool _attends;` with `[ObservableProperty] private string _status = "no";`
- Add computed `public bool IsAttending => Status == "yes";` with `[NotifyPropertyChangedFor(nameof(IsAttending))]` on `_status`
- `Attends = att.Attends` → `Status = att.Status`
- `SetAttends(bool value)` → `SetStatus(string value)`
- `new UpsertAttendanceRequest(Attends, ...)` → `new UpsertAttendanceRequest(Status, ...)`

**`Demizon.Maui/ViewModels/Attendance/AttendanceViewModel.cs`** (line 60)
- `e.MyAttendance is { Attends: true }` → `e.MyAttendance?.Status == "yes"`

**`Demizon.Maui/ViewModels/Attendance/MemberAttendanceDetailViewModel.cs`**
- Same pattern as EventDetailViewModel: `bool _attends` → `string _status = "no"`, update Load/Save/SetAttends

---

### 7. MAUI — XAML Pages

**`Demizon.Maui/Pages/EventDetailPage.xaml`**
- Change `ColumnDefinitions="*,*"` → `"*,*,*"` (3 columns)
- Add "? Nevím" middle button (AttendanceYellow, `SetStatusCommand` with `"maybe"`)
- Change button triggers from bool `{Binding Attends}` to string `{Binding Status}` value triggers:
  - BtnAttend: filled when `Status == "yes"`
  - BtnMaybe: filled when `Status == "maybe"`
  - BtnDecline: filled when `Status == "no"`
- Role picker: `IsVisible="{Binding IsAttending}"`

**`Demizon.Maui/Pages/Attendance/MemberAttendanceDetailPage.xaml`**
- Same 3-button layout as EventDetailPage

---

### 8. MAUI — Table + Converter

**`Demizon.Maui/Pages/Attendance/AllMembersAttendancePage.xaml.cs`** (`AddAttendanceCell`)
- 3-state symbol logic:
  - `Status == "yes"` → `"✓"` green
  - `Status == "maybe"` → `"?"` orange (`AttendanceYellow` #F39C12)
  - `Status == "no"` → `"✗"` red
  - `null/cancelled` → unchanged

**`Demizon.Maui/Converters/AttendanceColorConverter.cs`**
- Pattern match on `Status` string: `"yes"` → `AttendanceGreen`, `"maybe"` → `AttendanceYellow`, `"no"` → `AttendanceRed`, `_` → `AttendanceGray`

---

## Critical Files Summary

| Layer | File |
|-------|------|
| DAL | `Demizon.Dal/Entities/Attendance.cs` |
| DAL | `Demizon.Dal/DemizonContext.cs` |
| DAL | `Demizon.Dal/Migrations/` (new migration + snapshot update) |
| Contracts | `Demizon.Contracts/Attendances/AttendanceDto.cs` |
| Contracts | `Demizon.Contracts/Attendances/UpsertAttendanceRequest.cs` |
| Contracts | `Demizon.Contracts/Attendances/MemberCellDto.cs` |
| API | `Demizon.Api/Mapping/ContractMappingExtensions.cs` |
| API | `Demizon.Api/Controllers/AttendancesController.cs` |
| Core | `Demizon.Core/Services/Attendance/AttendanceReportService.cs` |
| MVC | `Demizon.Mvc/Services/AttendanceReminderBackgroundService.cs` |
| MVC | `Demizon.Mvc/ViewModels/AttendanceViewModel.cs` |
| MVC | `Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor` |
| MVC | `Demizon.Mvc/Pages/Admin/Attendance/MemberAttendance.razor.cs` |
| MVC | `Demizon.Mvc/Pages/Admin/Attendance/Components/AttendanceForm.razor` |
| MAUI | `Demizon.Maui/ViewModels/EventDetailViewModel.cs` |
| MAUI | `Demizon.Maui/ViewModels/Attendance/AttendanceViewModel.cs` |
| MAUI | `Demizon.Maui/ViewModels/Attendance/MemberAttendanceDetailViewModel.cs` |
| MAUI | `Demizon.Maui/Pages/EventDetailPage.xaml` |
| MAUI | `Demizon.Maui/Pages/Attendance/MemberAttendanceDetailPage.xaml` |
| MAUI | `Demizon.Maui/Pages/Attendance/AllMembersAttendancePage.xaml.cs` |
| MAUI | `Demizon.Maui/Converters/AttendanceColorConverter.cs` |

---

## Verification

1. **Build**: `dotnet build` on all projects — no compile errors
2. **Migration**: `dotnet ef database update` runs without error; existing data intact
3. **API test**: PUT `/api/attendances/{eventId}` with `{"status":"maybe","comment":null,"activityRole":null}` returns `{"status":"maybe",...}`
4. **Reminder test**: Member with Maybe status receives FCM notification on next check cycle
5. **MVC**: Attendance table shows `?` (yellow/Help icon) for Maybe; sum counts exclude Maybe from Σ
6. **MAUI**: EventDetail shows 3 buttons; "Nevím" highlights orange when tapped; table cell shows orange `?`
7. **Code review subagent**: Run after implementation to verify correctness

---

## Notes
- `AttendanceStatus` enum lives in `Demizon.Dal` — both API and MVC projects already reference Dal. No new project references needed.
- MAUI uses string `"yes"/"no"/"maybe"` (from contract DTOs) — no Dal enum reference needed in MAUI.
- The migration is a simple `RenameColumn` — no data transformation required.
- GCal sync for `Maybe`: treat same as `No` (delete calendar event if one exists). Rationale: "maybe" means undecided, don't pollute the calendar.
