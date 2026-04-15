# Demizon MAUI App Enhancement Plan - Implementation Guide

**Created:** 2026-04-15  
**Status:** Planning Phase  
**Project:** Demizon Mobile App (MAUI) - Attendance Feature Implementation  
**Language:** Czech-English (EN documentation)

---

## Executive Summary

This document outlines a comprehensive enhancement plan for the Demizon MAUI mobile application. The primary objectives are:

1. **Logo Integration**: Replace emoji placeholder with official FS Demižón logo
2. **Attendance Tracking**: Implement full attendance management feature (core functionality)
3. **Modern UI/UX**: Redesign interface with mobile-first principles and visual consistency
4. **Feature Parity**: Mirror key MVC admin features in mobile-optimized format

**Expected Outcome**: A fully functional mobile attendance tracking app that matches web admin functionality while providing excellent mobile UX.

---

## Current State Analysis

### MAUI App (Before)
- **Navigation**: Tab-based (Events, Dances, Profile)
- **Features**: Event browsing, dance viewing, user profile
- **Logo**: Emoji placeholder (🎻) in LoginPage
- **Styling**: Brown color palette (#A8845E primary), MAUI native controls
- **Auth**: JWT-based, token refresh support
- **Limitations**: No attendance tracking, minimal feature set

### MVC Web App (Reference)
- **Attendance Module**: Full-featured attendance tracking with:
  - Monthly calendar view with event attendance status
  - Gender-based filtering (Male/Female/All)
  - Attendance editing with comments and role selection
  - Statistics page with date range analysis
  - Color-coded percentage indicators (green 80+%, yellow 50+%, red <50%)
  - Member categorization (Dancers, Musicians, External)
  - Google Calendar integration
  - Hidden attendance toggle
- **Styling**: MudBlazor components, brown/cream color scheme
- **Design**: Desktop-focused, responsive CSS

---

## Implementation Overview

### Phase 1: Foundation & Logo (Priority: Critical)

**Objective**: Establish proper branding with official logo

#### Tasks
1. **Copy logo asset**
   - Source: `Demizon.Mvc\wwwroot\images\logo.jpg`
   - Destination: `Demizon.Maui\Assets\logo.jpg`
   - Format: PNG/XAML image resource in csproj

2. **Update LoginPage.xaml**
   - Replace emoji with `<Image Source="logo.jpg" ... />`
   - Proper sizing: ~120px diameter circular display
   - Add spacing and visual hierarchy
   - Maintain: Branding text "FS Demižón" + "Strážnice"

#### Estimated Effort: 1-2 hours

---

### Phase 2: Attendance Core - Data Layer (Priority: High)

**Objective**: Connect mobile app to existing attendance API endpoints

#### API Endpoints to Integrate

```csharp
// GET endpoint - fetch user attendances
GET /api/attendances/me
Authorization: Bearer {token}
Response: List<AttendanceDto>
{
  id: int,
  attends: bool,
  comment: string?,
  activityRole: string?,  // "Dancer" or "Musician"
  lastUpdated: DateTime,
  eventId: int,
  event: EventDto {
    id: int,
    name: string,
    dateFrom: DateTime,
    dateTo: DateTime,
    place: string,
    isCancelled: bool
  }
}

// PUT endpoint - update/create attendance
PUT /api/attendances/{eventId}
Authorization: Bearer {token}
Request: {
  attends: bool,
  comment: string?,
  activityRole: string?
}
Response: AttendanceDto
```

#### Tasks
1. **Extend IApiClient interface**
   - Add `Task<List<AttendanceDto>> GetUserAttendancesAsync()`
   - Add `Task<AttendanceDto> UpdateAttendanceAsync(int eventId, UpsertAttendanceRequest request)`
   - Add `Task<List<MemberAttendanceStatDto>> GetStatisticsAsync(DateTime from, DateTime to)`

2. **Create AttendanceService**
   ```csharp
   public class AttendanceService : IAttendanceService
   {
       Task<List<AttendanceDto>> GetUserAttendancesAsync(DateTime from, DateTime to)
       Task<AttendanceDto> UpdateAttendanceAsync(int eventId, UpsertAttendanceRequest request)
       Task<List<MemberAttendanceStatDto>> GetStatisticsAsync(DateTime from, DateTime to)
       List<AttendanceGroupDto> GroupByMonth(List<AttendanceDto> attendances)
       List<AttendanceGroupDto> FilterByGender(List<AttendanceGroupDto> groups, Gender? gender)
   }
   ```

#### Estimated Effort: 3-4 hours

---

### Phase 3: Attendance Core - ViewModels (Priority: High)

**Objective**: Implement MVVM ViewModels for attendance features

#### AttendanceListViewModel
```csharp
public partial class AttendanceListViewModel : BaseViewModel
{
    private readonly IAttendanceService _service;
    private readonly INavigationService _navigation;

    // Observable properties (MVVM Community Toolkit)
    [ObservableProperty]
    int currentMonth;
    
    [ObservableProperty]
    int currentYear;
    
    [ObservableProperty]
    ObservableCollection<AttendanceGroupDto> attendances;
    
    [ObservableProperty]
    Gender? selectedGender;
    
    [ObservableProperty]
    bool showHiddenAttendances;
    
    [ObservableProperty]
    bool isLoading;

    // Commands
    [RelayCommand]
    async Task PreviousMonth() { /* ... */ }
    
    [RelayCommand]
    async Task NextMonth() { /* ... */ }
    
    [RelayCommand]
    async Task FilterByGender(Gender? gender) { /* ... */ }
    
    [RelayCommand]
    async Task ToggleVisibility() { /* ... */ }
    
    [RelayCommand]
    async Task EditAttendance(AttendanceDto attendance) { /* ... */ }

    // Computed properties
    public int DancerCount { get; }
    public int MusicianCount { get; }
    public string MonthYearDisplay { get; }
}
```

#### AttendanceDetailViewModel
```csharp
public partial class AttendanceDetailViewModel : BaseViewModel
{
    [ObservableProperty]
    EventDto selectedEvent;
    
    [ObservableProperty]
    bool isAttending;
    
    [ObservableProperty]
    string? comment;
    
    [ObservableProperty]
    string selectedRole;  // "Dancer" or "Musician"
    
    [ObservableProperty]
    List<string> availableRoles;
    
    [ObservableProperty]
    bool isLoading;

    [RelayCommand]
    async Task Save() { /* ... */ }
    
    [RelayCommand]
    async Task Cancel() { /* ... */ }
    
    [RelayCommand]
    async Task Reset() { /* ... */ }
}
```

#### AttendanceStatsViewModel
```csharp
public partial class AttendanceStatsViewModel : BaseViewModel
{
    [ObservableProperty]
    DateTime fromDate;
    
    [ObservableProperty]
    DateTime toDate;
    
    [ObservableProperty]
    ObservableCollection<MemberAttendanceStatDto> statistics;
    
    [ObservableProperty]
    int totalRehearsals;
    
    [ObservableProperty]
    int totalActions;
    
    [ObservableProperty]
    bool isLoading;

    [RelayCommand]
    async Task UpdateDateRange() { /* ... */ }
}
```

#### Estimated Effort: 4-5 hours

---

### Phase 4: Attendance Core - UI Pages (Priority: High)

**Objective**: Create responsive XAML pages for attendance features

#### AttendanceListPage.xaml
- **Layout**: Vertical StackLayout with ScrollView for mobile
- **Header Section**:
  - Month/Year display with navigation buttons (prev/next)
  - Gender filter dropdown
  - "Show Hidden" checkbox toggle
- **Content Section**:
  - Grouped attendance by role (Dancers/Musicians)
  - Card-based layout for events:
    - Event name, date/time
    - Attendance status badge (✓ Attended / ✗ Not Attended / ? Pending)
    - Colored indicator (green/red/yellow)
    - Tap to edit
  - Summary row: attendance count per role
- **Interaction**: Tap event card to open detail page

#### AttendanceDetailPage.xaml
- **Layout**: Form-based with ScrollView
- **Event Info Section**:
  - Event name (large)
  - Date/Time, Location
  - Cancellation notice (if applicable)
- **Attendance Form**:
  - Checkbox: "Zúčastním se" (I will attend)
  - Conditional Role Picker: (if member is both dancer + musician)
  - Comment field (multiline)
  - Last updated timestamp
- **Action Buttons**:
  - Save (primary, blue)
  - Cancel
  - Reset (if existing attendance)
- **Binding**: Two-way binding to AttendanceDetailViewModel

#### AttendanceStatsPage.xaml
- **Layout**: ScrollView with StackLayout
- **Date Range Section**:
  - "From" date picker
  - "To" date picker
  - "Update" button
- **Summary Cards** (3 columns on tablet, stacked on mobile):
  - Card 1: "Total Rehearsals" + count
  - Card 2: "Total Actions" + count
  - Card 3: "Period" + date range
- **Statistics Table**:
  - Columns: Name, Rehearsals (count/%, color), Actions (count/%, color)
  - Color coding: 
    - 80%+ = green (#27AE60)
    - 50-79% = yellow (#F39C12)
    - <50% = red (#E74C3C)
  - Expandable row: click for details

#### Estimated Effort: 5-6 hours

---

### Phase 5: UI/UX Modernization (Priority: Medium)

**Objective**: Apply modern design principles and visual consistency

#### Color System Update (Colors.xaml)

```xml
<!-- Primary Brand Colors (existing) -->
<Color x:Key="Primary">#A8845E</Color>
<Color x:Key="PrimaryDark">#7A5A38</Color>
<Color x:Key="Secondary">#B89470</Color>
<Color x:Key="PageBackground">#FEFBF5</Color>

<!-- Semantic Colors (new) -->
<Color x:Key="Success">#27AE60</Color>    <!-- Green -->
<Color x:Key="Warning">#F39C12</Color>    <!-- Yellow -->
<Color x:Key="Error">#E74C3C</Color>      <!-- Red -->
<Color x:Key="Pending">#3498DB</Color>    <!-- Blue -->

<!-- Status Colors (refined) -->
<Color x:Key="AttendanceGreen">#27AE60</Color>
<Color x:Key="AttendanceRed">#E74C3C</Color>
<Color x:Key="AttendanceYellow">#F39C12</Color>

<!-- Text Colors -->
<Color x:Key="TextPrimary">#4A3420</Color>
<Color x:Key="TextSecondary">#8A6848</Color>
<Color x:Key="TextLight">#C9B5A3</Color>

<!-- Background Colors -->
<Color x:Key="CardBackground">#FFFFFF</Color>
<Color x:Key="CardBackgroundAlt">#F5F5F5</Color>
<Color x:Key="DividerColor">#E5DDD0</Color>
```

#### Style System Update (Styles.xaml)

```xml
<!-- Attendance Card Style -->
<Style TargetType="Frame" x:Key="AttendanceCardStyle">
    <Setter Property="CornerRadius" Value="12" />
    <Setter Property="BorderColor" Value="{StaticResource Primary}" />
    <Setter Property="Padding" Value="12,12" />
    <Setter Property="Margin" Value="0,6" />
    <Setter Property="HasShadow" Value="True" />
</Style>

<!-- Statistics Badge Style -->
<Style TargetType="Label" x:Key="StatsBadgeStyle">
    <Setter Property="FontSize" Value="14" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="CornerRadius" Value="8" />
</Style>

<!-- Large Heading Style -->
<Style TargetType="Label" x:Key="HeadingLargeStyle">
    <Setter Property="FontSize" Value="28" />
    <Setter Property="FontAttributes" Value="Bold" />
    <Setter Property="TextColor" Value="{StaticResource TextPrimary}" />
    <Setter Property="Margin" Value="0,8" />
</Style>

<!-- Touch-friendly Button Style -->
<Style TargetType="Button" x:Key="PrimaryButtonStyle">
    <Setter Property="CornerRadius" Value="12" />
    <Setter Property="Padding" Value="16,12" />
    <Setter Property="FontSize" Value="16" />
    <Setter Property="MinimumHeightRequest" Value="44" />
    <Setter Property="MinimumWidthRequest" Value="100" />
</Style>
```

#### LoginPage Redesign
- **Hero Section**:
  - Logo image (120x120px, centered)
  - Branding text: "FS Demižón" (24pt, bold)
  - Subtitle: "Strážnice" (14pt, secondary color)
  - Spacing: 24px below image
- **Form Section**:
  - Login label (12pt, secondary)
  - Entry field with placeholder
  - Password label (12pt, secondary)
  - Entry field with eye toggle
  - Remember Me checkbox
- **Action**:
  - Login button (full width, 44px height)
  - Error message display (red, 12pt)
  - Loading indicator during auth
- **Footer**:
  - Version info (8pt, light gray)

#### Responsive Design Principles
- **Mobile First** (320px+): Single column, large touch targets
- **Tablet** (600px+): Multi-column layouts for statistics table
- **Landscape**: Adjusted padding/margins, horizontal scroll enabled for dense tables
- **Accessibility**: Minimum 44x44px touch targets, high contrast text

#### Estimated Effort: 4-5 hours

---

### Phase 6: Navigation & Integration (Priority: High)

**Objective**: Update app navigation and register services

#### AppShell.xaml Changes
```xml
<TabBar>
    <ShellContent Title="Docházka" Icon="attendance" Route="AttendanceListPage">
        <local:AttendanceListPage />
    </ShellContent>
    <ShellContent Title="Akce" Icon="events" Route="EventsPage">
        <local:EventsPage />
    </ShellContent>
    <ShellContent Title="Tance" Icon="dances" Route="DancesPage">
        <local:DancesPage />
    </ShellContent>
    <ShellContent Title="Profil" Icon="profile" Route="ProfilePage">
        <local:ProfilePage />
    </ShellContent>
</TabBar>
```

#### MauiProgram.cs Changes
```csharp
builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
builder.Services.AddTransient<AttendanceListPage>();
builder.Services.AddTransient<AttendanceListViewModel>();
builder.Services.AddTransient<AttendanceDetailPage>();
builder.Services.AddTransient<AttendanceDetailViewModel>();
builder.Services.AddTransient<AttendanceStatsPage>();
builder.Services.AddTransient<AttendanceStatsViewModel>();
```

#### Navigation Routes
- `attendance` → AttendanceListPage (landing page after login)
- `attendance/detail/{eventId}` → AttendanceDetailPage
- `attendance/stats` → AttendanceStatsPage (accessible from list)

#### Estimated Effort: 2 hours

---

### Phase 7: Testing & Validation (Priority: High)

**Objective**: Ensure feature quality and no regressions

#### API Integration Testing
- [ ] GET `/api/attendances/me` returns correct data
- [ ] PUT `/api/attendances/{eventId}` updates successfully
- [ ] GET `/api/attendances/stats` calculates correctly
- [ ] Error handling (401 Unauthorized, 404 Not Found, 500 Server Error)
- [ ] Token refresh on 401 response

#### Feature Testing
- [ ] AttendanceListPage:
  - [ ] Month navigation (prev/next)
  - [ ] Gender filter (All/Female/Male/None)
  - [ ] Hidden attendance toggle
  - [ ] Tap event to edit
- [ ] AttendanceDetailPage:
  - [ ] Save attendance (attended)
  - [ ] Save attendance (not attended)
  - [ ] Add/edit comment
  - [ ] Role selection (if applicable)
  - [ ] Cancel and Reset buttons work
- [ ] AttendanceStatsPage:
  - [ ] Date range picker
  - [ ] Statistics calculation
  - [ ] Color coding accuracy (green/yellow/red)
  - [ ] Percentage display

#### UI/UX Testing
- [ ] Responsive layout on 5.5" phone, 7" tablet
- [ ] Landscape orientation layout
- [ ] Dark mode compatibility (if applicable)
- [ ] Touch target sizing (min 44px)
- [ ] Typography readability

#### Regression Testing
- [ ] Login flow works
- [ ] Token refresh successful
- [ ] Events page loads and filters work
- [ ] Dances page loads and displays correctly
- [ ] Profile page shows user info
- [ ] Navigation between tabs smooth

#### Performance Testing
- [ ] AttendanceListPage: <2s load time
- [ ] AttendanceStatsPage: <3s load time (stats calculation)
- [ ] No memory leaks (profile with DevTools)
- [ ] Battery impact acceptable

#### Estimated Effort: 4-5 hours

---

### Phase 8: Documentation & Deployment (Priority: Medium)

**Objective**: Document implementation and prepare for production

#### Technical Documentation
- API integration details
- ViewModel architecture
- Data binding patterns
- Performance optimizations
- Caching strategy (if implemented)

#### User Documentation
- Feature overview (Czech)
- How to edit attendance
- Statistics interpretation
- Filter usage
- Troubleshooting

#### Estimated Effort: 2 hours

---

## Technical Architecture

### Data Flow Diagram

```
[Mobile App]
    ↓
[IApiClient] (Refit HTTP client)
    ↓
[JWT Token + AuthHandler]
    ↓
[API Endpoints]
    ↓
[Backend Services (IAttendanceService)]
    ↓
[Database (SQLite)]
```

### MVVM Component Hierarchy

```
AppShell
├── AttendanceListPage
│   ├── AttendanceListViewModel
│   │   ├── IAttendanceService
│   │   └── INavigationService
│   └── Views
│       ├── MonthNavigator
│       ├── FilterBar
│       └── AttendanceGrid
├── AttendanceDetailPage
│   ├── AttendanceDetailViewModel
│   │   ├── IAttendanceService
│   │   └── INavigationService
│   └── Views
│       └── AttendanceForm
└── AttendanceStatsPage
    ├── AttendanceStatsViewModel
    │   ├── IAttendanceService
    │   └── DateRangePicker
    └── Views
        ├── SummaryCards
        └── StatisticsTable
```

---

## File Structure

### New Files to Create
```
Demizon.Maui/
├── Pages/
│   └── Attendance/
│       ├── AttendanceListPage.xaml
│       ├── AttendanceListPage.xaml.cs
│       ├── AttendanceDetailPage.xaml
│       ├── AttendanceDetailPage.xaml.cs
│       ├── AttendanceStatsPage.xaml
│       └── AttendanceStatsPage.xaml.cs
├── ViewModels/
│   └── Attendance/
│       ├── AttendanceListViewModel.cs
│       ├── AttendanceDetailViewModel.cs
│       └── AttendanceStatsViewModel.cs
├── Services/
│   ├── IAttendanceService.cs
│   └── AttendanceService.cs
├── Assets/
│   └── logo.jpg (copied from MVC)
└── Resources/
    └── Styles/
        ├── Colors.xaml (updated)
        └── Styles.xaml (updated)
```

### Modified Files
```
Demizon.Maui/
├── AppShell.xaml (add Attendance tab)
├── Pages/
│   └── LoginPage.xaml (update logo)
├── Services/
│   └── IApiClient.cs (add attendance endpoints)
├── Resources/
│   └── Styles/
│       ├── Colors.xaml (add semantic colors)
│       └── Styles.xaml (add new component styles)
└── MauiProgram.cs (register services)
```

---

## Success Criteria & Metrics

### Functional Criteria
- ✓ Logo displays in LoginPage
- ✓ All attendance endpoints integrated
- ✓ Monthly calendar view functional
- ✓ Editing attendance works with API sync
- ✓ Statistics calculated and displayed
- ✓ Filters (gender, visibility) work correctly
- ✓ Navigation smooth between pages

### Quality Criteria
- ✓ No API errors in production logs
- ✓ Page load time <2s (mobile network)
- ✓ Memory usage <100MB
- ✓ Responsive on 5.5"-7" devices
- ✓ All touch targets ≥44px
- ✓ No visual artifacts on landscape

### User Acceptance Criteria
- ✓ Users can view their monthly attendance
- ✓ Users can edit attendance for events
- ✓ Statistics clearly show attendance rates
- ✓ App matches MVC functionality
- ✓ Design is modern and professional

---

## Timeline Estimates

| Phase | Task Count | Estimated Hours | Days (8h/day) |
|-------|-----------|-----------------|---------------|
| Phase 1 (Logo) | 2 | 1.5 | 0.2 |
| Phase 2 (Data Layer) | 2 | 3.5 | 0.4 |
| Phase 3 (ViewModels) | 3 | 4.5 | 0.6 |
| Phase 4 (UI Pages) | 3 | 5.5 | 0.7 |
| Phase 5 (UI Design) | 5 | 4.5 | 0.6 |
| Phase 6 (Integration) | 2 | 2 | 0.25 |
| Phase 7 (Testing) | 6 | 4.5 | 0.6 |
| Phase 8 (Docs) | 2 | 2 | 0.25 |
| **TOTAL** | **25** | **28** | **3.7** |

**Recommended Timeline**: 4-5 business days with 1 person, or 2-3 days with 2 developers

---

## Risk Mitigation

### Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| API endpoint mismatch | High | Early API testing, verify endpoints with backend team |
| Layout responsive issues | Medium | Test on multiple device sizes early (Phase 4) |
| Performance on old devices | Medium | Profile with DevTools, optimize images and data queries |
| Token refresh failures | High | Implement robust token refresh with retry logic |
| Google Calendar conflicts | Low | Document sync behavior, clear error messages |

---

## Dependencies & Prerequisites

### Required NuGet Packages
- `CommunityToolkit.Mvvm` ≥ 8.2 (already present)
- `Refit` ≥ 6.3 (already present)
- `Microsoft.Maui.Controls` ≥ 8.0 (already present)

### Optional Enhancements
- `NodaTime` (for robust date handling)
- `PropertyChanged.Fody` (for lighter MVVM)
- `ZXing.Net.Maui` (for barcode attendance scanning, future)

### Backend Requirements
- Attendance API endpoints implemented and tested
- Google Calendar OAuth properly configured (if using sync)
- Database migrations applied

---

## Localization Notes

The app should support Czech (CS) and English (EN) languages. Key strings for translation:

**Czech (CS)**
- "Docházka" → Attendance
- "Zúčastním se" → I will attend
- "Komentář" → Comment
- "Rola" → Role
- "Tanečník" → Dancer
- "Hudebník" → Musician
- "Skrytá docházka" → Hidden attendances
- "Srpen 2026" → August 2026
- "Statistika" → Statistics

---

## Future Enhancements (Phase 2+)

1. **Offline Support**: Cache attendance data for offline browsing
2. **Notifications**: Push notifications before events
3. **Barcode Scanning**: Quick attendance via QR codes
4. **Export**: PDF/CSV export of attendance statistics
5. **Dark Mode**: MAUI dark theme support
6. **Biometric Auth**: Fingerprint/Face ID login
7. **Analytics**: User engagement metrics
8. **Sync**: Real-time sync with multiple devices

---

## References

### Source Files
- Attendance API: `Demizon.Api\Controllers\AttendancesController.cs`
- Attendance Service: `Demizon.Core\Services\Attendance\IAttendanceService.cs`
- MVC Attendance UI: `Demizon.Mvc\Pages\Admin\Attendance\`
- Database Models: `Demizon.Dal\Entities\Attendance.cs`, `Member.cs`, `Event.cs`
- Logo Asset: `Demizon.Mvc\wwwroot\images\logo.jpg`

### Related Documentation
- MAUI Docs: https://learn.microsoft.com/en-us/dotnet/maui/
- Refit Docs: https://github.com/reactiveui/refit
- MVVM Community Toolkit: https://learn.microsoft.com/en-us/windows/communitytoolkit/mvvm/introduction
- MudBlazor Reference (for design patterns): https://mudblazor.com/

---

**Document Version**: 1.0  
**Last Updated**: 2026-04-15  
**Owner**: Development Team  
**Status**: Ready for Implementation
