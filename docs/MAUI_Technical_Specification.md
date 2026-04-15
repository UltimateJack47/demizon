# Demizon MAUI - Technical Specification
## Attendance Feature Implementation Details

**Document**: Technical Specification  
**Date**: 2026-04-15  
**Language**: English  
**Status**: For Implementation

---

## 1. API Contract Specification

### Endpoint 1: Get User Attendances

**Method**: `GET`  
**Path**: `/api/attendances/me`  
**Authentication**: Required (Bearer Token)  
**Rate Limit**: Standard (not rate-limited)

**Request Parameters**: None in body

**Query Parameters** (Optional):
```
?from=2026-01-01&to=2026-12-31   // ISO 8601 date format
```

**Response**: HTTP 200 OK
```json
{
  "data": [
    {
      "id": 1,
      "attends": true,
      "comment": "Nemohl jsem přijet dříve",
      "activityRole": "Dancer",
      "lastUpdated": "2026-04-10T14:30:00Z",
      "eventId": 5,
      "event": {
        "id": 5,
        "name": "Páteční zkouška",
        "dateFrom": "2026-04-17T19:00:00Z",
        "dateTo": "2026-04-17T21:00:00Z",
        "place": "Sál sv. Jiří",
        "isCancelled": false
      }
    },
    {
      "id": 2,
      "attends": false,
      "comment": null,
      "activityRole": "Musician",
      "lastUpdated": "2026-03-20T10:15:00Z",
      "eventId": 4,
      "event": {
        "id": 4,
        "name": "Soutěž",
        "dateFrom": "2026-04-10T09:00:00Z",
        "dateTo": "2026-04-10T17:00:00Z",
        "place": "Pardubice",
        "isCancelled": false
      }
    }
  ]
}
```

**Error Responses**:
- `401 Unauthorized`: Invalid/expired token → trigger token refresh
- `404 Not Found`: User/attendances not found
- `500 Internal Server Error`: Server error

---

### Endpoint 2: Update Attendance

**Method**: `PUT`  
**Path**: `/api/attendances/{eventId}`  
**Authentication**: Required (Bearer Token)  
**Rate Limit**: Standard

**Path Parameter**:
```
eventId: integer (required)   // Event ID to create/update attendance for
```

**Request Body**:
```json
{
  "attends": true,
  "comment": "Será budu tam!",
  "activityRole": "Dancer"
}
```

**Response**: HTTP 200 OK
```json
{
  "id": 1,
  "attends": true,
  "comment": "Será budu tam!",
  "activityRole": "Dancer",
  "lastUpdated": "2026-04-15T13:00:00Z",
  "eventId": 5,
  "event": {
    "id": 5,
    "name": "Páteční zkouška",
    "dateFrom": "2026-04-17T19:00:00Z",
    "dateTo": "2026-04-17T21:00:00Z",
    "place": "Sál sv. Jiří",
    "isCancelled": false
  }
}
```

**Error Responses**:
- `400 Bad Request`: Invalid role (not "Dancer" or "Musician")
- `401 Unauthorized`: Invalid/expired token
- `403 Forbidden`: Event in past or cancelled
- `404 Not Found`: Event not found
- `500 Internal Server Error`: Server error / Google Calendar sync failed

---

### Endpoint 3: Get Attendance Statistics (Future)

**Method**: `GET`  
**Path**: `/api/attendances/stats`  
**Authentication**: Required (Bearer Token)

**Query Parameters**:
```
?from=2026-01-01&to=2026-12-31
```

**Response**: HTTP 200 OK
```json
{
  "data": {
    "period": {
      "from": "2026-01-01",
      "to": "2026-12-31"
    },
    "summary": {
      "totalRehearsals": 40,
      "totalActions": 8,
      "totalEventsDays": 48
    },
    "memberStats": [
      {
        "memberId": 1,
        "fullName": "Josef Obrtlík",
        "rehearsalsAttended": 35,
        "rehearsalsTotal": 40,
        "rehearsalRate": 0.875,
        "actionsAttended": 7,
        "actionsTotal": 8,
        "actionRate": 0.875
      }
    ]
  }
}
```

---

## 2. Data Models

### AttendanceDto
```csharp
public record AttendanceDto(
    int Id,
    bool Attends,
    string? Comment,
    string? ActivityRole,
    DateTime LastUpdated,
    int EventId,
    EventDto Event
);
```

### UpsertAttendanceRequest
```csharp
public record UpsertAttendanceRequest(
    bool Attends,
    string? Comment,
    string? ActivityRole
);
```

### EventDto
```csharp
public record EventDto(
    int Id,
    string Name,
    DateTime DateFrom,
    DateTime DateTo,
    string Place,
    bool IsCancelled
);
```

### AttendanceGroupDto (for grouping by month/role)
```csharp
public class AttendanceGroupDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; }
    public List<AttendanceDto> Attendances { get; set; }
    
    // Computed
    public int DancerCount { get; set; }
    public int MusicianCount { get; set; }
    public int TotalCount => DancerCount + MusicianCount;
}
```

### MemberAttendanceStatDto
```csharp
public record MemberAttendanceStatDto(
    int MemberId,
    string FullName,
    int RehearsalsAttended,
    int RehearsalsTotal,
    decimal RehearsalRate,
    int ActionsAttended,
    int ActionsTotal,
    decimal ActionRate
);
```

---

## 3. Service Interfaces

### IApiClient (Refit Interface Update)

```csharp
public interface IApiClient
{
    // Existing endpoints...
    
    // New Attendance endpoints
    [Get("/api/attendances/me")]
    Task<ApiResponse<List<AttendanceDto>>> GetUserAttendancesAsync();
    
    [Put("/api/attendances/{eventId}")]
    Task<ApiResponse<AttendanceDto>> UpdateAttendanceAsync(
        int eventId,
        [Body] UpsertAttendanceRequest request
    );
    
    [Get("/api/attendances/stats")]
    Task<ApiResponse<AttendanceStatsResponseDto>> GetStatisticsAsync(
        [Query] DateTime from,
        [Query] DateTime to
    );
}
```

### IAttendanceService

```csharp
public interface IAttendanceService
{
    /// <summary>
    /// Fetch all attendances for logged-in user in a date range.
    /// </summary>
    Task<List<AttendanceDto>> GetUserAttendancesAsync(DateTime from, DateTime to);
    
    /// <summary>
    /// Update or create an attendance record for an event.
    /// </summary>
    Task<AttendanceDto> UpdateAttendanceAsync(
        int eventId,
        UpsertAttendanceRequest request
    );
    
    /// <summary>
    /// Get attendance statistics for a date range.
    /// </summary>
    Task<List<MemberAttendanceStatDto>> GetStatisticsAsync(
        DateTime from,
        DateTime to
    );
    
    /// <summary>
    /// Group attendances by month for display.
    /// </summary>
    List<AttendanceGroupDto> GroupByMonth(List<AttendanceDto> attendances);
    
    /// <summary>
    /// Filter attendance groups by gender (if applicable to user).
    /// </summary>
    List<AttendanceGroupDto> FilterByGender(
        List<AttendanceGroupDto> groups,
        string? genderFilter
    );
}
```

### AttendanceService (Implementation)

```csharp
public class AttendanceService : IAttendanceService
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(IApiClient apiClient, ILogger<AttendanceService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<List<AttendanceDto>> GetUserAttendancesAsync(
        DateTime from,
        DateTime to
    )
    {
        try
        {
            var response = await _apiClient.GetUserAttendancesAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"API returned {response.StatusCode}");
                return new List<AttendanceDto>();
            }

            var attendances = response.Content ?? new List<AttendanceDto>();
            return attendances
                .Where(a => a.Event.DateFrom.Date >= from.Date && 
                           a.Event.DateFrom.Date <= to.Date)
                .OrderByDescending(a => a.Event.DateFrom)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching attendances");
            throw;
        }
    }

    public async Task<AttendanceDto> UpdateAttendanceAsync(
        int eventId,
        UpsertAttendanceRequest request
    )
    {
        try
        {
            var response = await _apiClient.UpdateAttendanceAsync(eventId, request);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API error: {response.StatusCode}");
            }

            return response.Content!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating attendance for event {eventId}");
            throw;
        }
    }

    public async Task<List<MemberAttendanceStatDto>> GetStatisticsAsync(
        DateTime from,
        DateTime to
    )
    {
        try
        {
            var response = await _apiClient.GetStatisticsAsync(from, to);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<MemberAttendanceStatDto>();
            }

            return response.Content?.MemberStats ?? new List<MemberAttendanceStatDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statistics");
            throw;
        }
    }

    public List<AttendanceGroupDto> GroupByMonth(List<AttendanceDto> attendances)
    {
        return attendances
            .GroupBy(a => new { a.Event.DateFrom.Year, a.Event.DateFrom.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g => new AttendanceGroupDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthName = new DateTime(g.Key.Year, g.Key.Month, 1)
                    .ToString("MMMM yyyy", new CultureInfo("cs-CZ")),
                Attendances = g.ToList(),
                DancerCount = g.Count(a => a.ActivityRole == "Dancer"),
                MusicianCount = g.Count(a => a.ActivityRole == "Musician")
            })
            .ToList();
    }

    public List<AttendanceGroupDto> FilterByGender(
        List<AttendanceGroupDto> groups,
        string? genderFilter
    )
    {
        if (string.IsNullOrEmpty(genderFilter) || genderFilter == "All")
            return groups;

        // Implementation depends on how member roles map to gender
        // This is a simplified version
        return groups;
    }
}
```

---

## 4. ViewModel Specifications

### AttendanceListViewModel

```csharp
public partial class AttendanceListViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendanceService;
    private readonly INavigationService _navigationService;

    public AttendanceListViewModel(
        IAttendanceService attendanceService,
        INavigationService navigationService
    )
    {
        _attendanceService = attendanceService;
        _navigationService = navigationService;
        CurrentMonth = DateTime.Now.Month;
        CurrentYear = DateTime.Now.Year;
    }

    [ObservableProperty]
    private int currentMonth;

    [ObservableProperty]
    private int currentYear;

    [ObservableProperty]
    private ObservableCollection<AttendanceGroupDto> attendances = new();

    [ObservableProperty]
    private string? selectedGender;

    [ObservableProperty]
    private bool showHiddenAttendances;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    // Computed Properties
    public int DancerCount =>
        Attendances.FirstOrDefault()?.DancerCount ?? 0;

    public int MusicianCount =>
        Attendances.FirstOrDefault()?.MusicianCount ?? 0;

    public string MonthYearDisplay =>
        new DateTime(CurrentYear, CurrentMonth, 1)
            .ToString("MMMM yyyy", new CultureInfo("cs-CZ"));

    [RelayCommand]
    public async Task LoadAttendances()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var from = new DateTime(CurrentYear, CurrentMonth, 1);
            var to = from.AddMonths(1).AddDays(-1);

            var attendances = await _attendanceService
                .GetUserAttendancesAsync(from, to);
            var grouped = _attendanceService.GroupByMonth(attendances);

            Attendances = new ObservableCollection<AttendanceGroupDto>(grouped);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Nepodařilo se načíst docházku. Zkuste později.";
            Debug.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PreviousMonth()
    {
        if (CurrentMonth == 1)
        {
            CurrentMonth = 12;
            CurrentYear--;
        }
        else
        {
            CurrentMonth--;
        }

        await LoadAttendances();
    }

    [RelayCommand]
    public async Task NextMonth()
    {
        if (CurrentMonth == 12)
        {
            CurrentMonth = 1;
            CurrentYear++;
        }
        else
        {
            CurrentMonth++;
        }

        await LoadAttendances();
    }

    [RelayCommand]
    public async Task FilterByGender(string? gender)
    {
        SelectedGender = gender;
        // Refilter attendances if needed
        await LoadAttendances();
    }

    [RelayCommand]
    public async Task EditAttendance(AttendanceDto attendance)
    {
        await _navigationService.GoToAsync(
            $"attendance/detail/{attendance.EventId}",
            new Dictionary<string, object>
            {
                { "attendance", attendance }
            }
        );
    }
}
```

### AttendanceDetailViewModel

```csharp
public partial class AttendanceDetailViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceDetailViewModel(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [ObservableProperty]
    private EventDto? selectedEvent;

    [ObservableProperty]
    private bool isAttending;

    [ObservableProperty]
    private string? comment;

    [ObservableProperty]
    private string selectedRole = "Dancer";

    [ObservableProperty]
    private ObservableCollection<string> availableRoles = new()
    {
        "Dancer",
        "Musician"
    };

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    private int _eventId;

    public void Initialize(int eventId)
    {
        _eventId = eventId;
    }

    [RelayCommand]
    public async Task Save()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var request = new UpsertAttendanceRequest(
                IsAttending,
                Comment,
                SelectedRole
            );

            await _attendanceService.UpdateAttendanceAsync(_eventId, request);

            // Navigate back
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Chyba při ukládání. Zkuste později.";
            Debug.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    public void Reset()
    {
        IsAttending = false;
        Comment = null;
        SelectedRole = "Dancer";
        ErrorMessage = null;
    }
}
```

### AttendanceStatsViewModel

```csharp
public partial class AttendanceStatsViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceStatsViewModel(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
        FromDate = DateTime.Now.AddMonths(-3);
        ToDate = DateTime.Now;
    }

    [ObservableProperty]
    private DateTime fromDate;

    [ObservableProperty]
    private DateTime toDate;

    [ObservableProperty]
    private ObservableCollection<MemberAttendanceStatDto> statistics = new();

    [ObservableProperty]
    private int totalRehearsals;

    [ObservableProperty]
    private int totalActions;

    [ObservableProperty]
    private bool isLoading;

    [RelayCommand]
    public async Task LoadStatistics()
    {
        IsLoading = true;

        try
        {
            var stats = await _attendanceService
                .GetStatisticsAsync(FromDate, ToDate);
            
            Statistics = new ObservableCollection<MemberAttendanceStatDto>(stats);

            // Calculate totals (from API response)
            TotalRehearsals = stats
                .FirstOrDefault()?.RehearsalsTotal ?? 0;
            TotalActions = stats
                .FirstOrDefault()?.ActionsTotal ?? 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UpdateDateRange()
    {
        await LoadStatistics();
    }

    public string GetStatColor(decimal rate)
    {
        return rate switch
        {
            >= 0.8m => "#27AE60",  // Green
            >= 0.5m => "#F39C12",  // Yellow
            _ => "#E74C3C"         // Red
        };
    }
}
```

---

## 5. XAML Page Specifications

### AttendanceListPage.xaml Structure

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             Title="Docházka">
    <VerticalStackLayout Spacing="16" Padding="16">
        <!-- Header: Month Navigation -->
        <HorizontalStackLayout HorizontalOptions="Center" Spacing="8">
            <ImageButton Command="{Binding PreviousMonthCommand}" 
                        Source="previous_arrow" />
            <Label Text="{Binding MonthYearDisplay}" 
                   FontSize="18" FontAttributes="Bold" />
            <ImageButton Command="{Binding NextMonthCommand}" 
                        Source="next_arrow" />
        </HorizontalStackLayout>

        <!-- Filters -->
        <Picker ItemsSource="{Binding Genders}"
                SelectedItem="{Binding SelectedGender}"
                Title="Filtr podle pohlaví" />
        
        <CheckBox IsChecked="{Binding ShowHiddenAttendances}" 
                  Text="Skrytá docházka" />

        <!-- Loading Indicator -->
        <ActivityIndicator IsRunning="{Binding IsLoading}" 
                          IsVisible="{Binding IsLoading}" />

        <!-- Error Message -->
        <Label Text="{Binding ErrorMessage}" 
               TextColor="Red" 
               IsVisible="{Binding ErrorMessage, Converter={StaticResource StringToBoolConverter}}" />

        <!-- Attendance Summary -->
        <Frame CornerRadius="12" HasShadow="True" Padding="12">
            <HorizontalStackLayout Spacing="16">
                <VerticalStackLayout HorizontalOptions="FillAndExpand">
                    <Label Text="{Binding DancerCount}" 
                           FontSize="20" FontAttributes="Bold" />
                    <Label Text="Tanečníci" FontSize="12" />
                </VerticalStackLayout>
                <VerticalStackLayout HorizontalOptions="FillAndExpand">
                    <Label Text="{Binding MusicianCount}" 
                           FontSize="20" FontAttributes="Bold" />
                    <Label Text="Hudebníci" FontSize="12" />
                </VerticalStackLayout>
            </HorizontalStackLayout>
        </Frame>

        <!-- Attendance List -->
        <CollectionView ItemsSource="{Binding Attendances}"
                       SelectionMode="Single"
                       SelectionChangedCommand="{Binding EditAttendanceCommand}"
                       SelectionChangedCommandParameter="{Binding SelectedItem, Source={RelativeSource Self}}">
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Frame CornerRadius="12" HasShadow="True" 
                          Margin="0,8" Padding="12">
                        <Grid RowDefinitions="*,*" Spacing="8">
                            <Label Text="{Binding Event.Name}" 
                                   FontSize="16" FontAttributes="Bold" />
                            <Label Text="{Binding Event.DateFrom, StringFormat='{0:dd.MM.yyyy HH:mm}'}" 
                                   FontSize="12" 
                                   Grid.Row="1" />
                            <Label Text="{Binding Event.Place}" 
                                   FontSize="11" 
                                   Grid.Row="1" />
                        </Grid>
                    </Frame>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </VerticalStackLayout>
</ContentPage>
```

---

## 6. Color & Style Constants

### Colors.xaml Update

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui">
    <!-- Primary Brand (Keep existing) -->
    <Color x:Key="Primary">#A8845E</Color>
    <Color x:Key="PrimaryDark">#7A5A38</Color>
    <Color x:Key="Secondary">#B89470</Color>

    <!-- Semantic Colors (Add new) -->
    <Color x:Key="Success">#27AE60</Color>
    <Color x:Key="Warning">#F39C12</Color>
    <Color x:Key="Error">#E74C3C</Color>
    <Color x:Key="Info">#3498DB</Color>

    <!-- Status Colors -->
    <Color x:Key="AttendanceGreen">#27AE60</Color>
    <Color x:Key="AttendanceRed">#E74C3C</Color>
    <Color x:Key="AttendanceYellow">#F39C12</Color>

    <!-- Backgrounds -->
    <Color x:Key="PageBackground">#FEFBF5</Color>
    <Color x:Key="CardBackground">#FFFFFF</Color>
    <Color x:Key="SurfaceAlt">#F5F5F5</Color>

    <!-- Text Colors -->
    <Color x:Key="TextPrimary">#4A3420</Color>
    <Color x:Key="TextSecondary">#8A6848</Color>
    <Color x:Key="TextTertiary">#C9B5A3</Color>
</ResourceDictionary>
```

---

## 7. Converter Specifications

### AttendanceStatColorConverter

```csharp
public class AttendanceStatColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal rate)
        {
            return rate switch
            {
                >= 0.8m => Color.FromHex("#27AE60"),  // Green
                >= 0.5m => Color.FromHex("#F39C12"),  // Yellow
                _ => Color.FromHex("#E74C3C")         // Red
            };
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

---

## 8. Integration Checklist

- [ ] Copy `logo.jpg` to `Demizon.Maui\Assets\`
- [ ] Update `IApiClient.cs` with attendance endpoints
- [ ] Implement `AttendanceService.cs`
- [ ] Create `AttendanceListViewModel.cs`
- [ ] Create `AttendanceDetailViewModel.cs`
- [ ] Create `AttendanceStatsViewModel.cs`
- [ ] Create `AttendanceListPage.xaml` and `.xaml.cs`
- [ ] Create `AttendanceDetailPage.xaml` and `.xaml.cs`
- [ ] Create `AttendanceStatsPage.xaml` and `.xaml.cs`
- [ ] Update `AppShell.xaml` with Attendance tab
- [ ] Update `LoginPage.xaml` with logo image
- [ ] Update `Colors.xaml` with semantic colors
- [ ] Update `Styles.xaml` with component styles
- [ ] Update `MauiProgram.cs` with service registrations
- [ ] Add `AttendanceStatColorConverter.cs`
- [ ] Test API integration
- [ ] Test responsive layouts
- [ ] Test attendance flows

---

**End of Technical Specification**
