using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels.Attendance;

/// <summary>
/// Monthly attendance overview — shows all events in the selected month
/// with the current user's attendance status for each.
/// </summary>
public partial class AttendanceViewModel : ObservableObject
{
    private static readonly CultureInfo CsCulture = new("cs-CZ");

    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigation;

    public AttendanceViewModel(IApiClient apiClient, INavigationService navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
        var today = DateTime.Today;
        _currentYear = today.Year;
        _currentMonth = today.Month;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthLabel))]
    private int _currentYear;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthLabel))]
    private int _currentMonth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(AttendedCount))]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    private ObservableCollection<EventDto> _events = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _errorMessage;

    public string MonthLabel => new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM yyyy", CsCulture);
    public bool HasEvents => Events.Count > 0;
    public bool IsEmpty => !IsBusy && Events.Count == 0;
    public int AttendedCount => Events.Count(e => e.MyAttendance is { Attends: true });
    public int TotalCount => Events.Count(e => !e.IsCancelled);

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetEventsByMonthAsync(CurrentYear, CurrentMonth);
            Events = new ObservableCollection<EventDto>(result);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst docházku.";
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        // RefreshView already set IsRefreshing=true before the command fires, so we must NOT
        // check IsRefreshing here — it would always bail and leave the spinner stuck.
        if (IsBusy)
        {
            IsRefreshing = false; // stop the spinner if a load is already in progress
            return;
        }
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetEventsByMonthAsync(CurrentYear, CurrentMonth);
            Events = new ObservableCollection<EventDto>(result);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst docházku.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task PreviousMonth()
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
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonth()
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
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateToEvent(EventDto eventDto)
    {
        if (eventDto.Id == 0)
        {
            // Rehearsal: navigate with date instead of eventId
            var date = eventDto.DateFrom.ToString("yyyy-MM-dd");
            await _navigation.GoToAsync($"{AppRoutes.EventDetail}?rehearsalDate={date}");
        }
        else
        {
            await _navigation.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventDto.Id}");
        }
    }

    [RelayCommand]
    private async Task NavigateToStats()
    {
        try
        {
            await _navigation.GoToAsync(AppRoutes.AttdStats);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít statistiky.";
        }
    }

    [RelayCommand]
    private async Task NavigateToOverview()
    {
        try
        {
            await _navigation.GoToAsync(AppRoutes.AttdOverview);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít přehled docházky.";
        }
    }
}