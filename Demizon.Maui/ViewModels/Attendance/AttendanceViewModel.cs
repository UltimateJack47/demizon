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
[QueryProperty(nameof(CurrentYear), "year")]
[QueryProperty(nameof(CurrentMonth), "month")]
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

    partial void OnCurrentYearChanged(int value)
    {
        if (_isInitialized) LoadCommand.Execute(null);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthLabel))]
    private int _currentMonth;

    partial void OnCurrentMonthChanged(int value)
    {
        if (_isInitialized) LoadCommand.Execute(null);
    }

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
    public int AttendedCount => Events.Count(e => e.MyAttendance?.Status == "yes");
    public int TotalCount => Events.Count(e => !e.IsCancelled);

    private bool _isInitialized;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        _isInitialized = true;

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
    private async Task NavigateToEvent(EventDto? eventDto)
    {
        if (eventDto is null) return;

        try
        {
            if (eventDto.Id == 0)
            {
                var date = eventDto.DateFrom.ToString("yyyy-MM-dd");
                await _navigation.GoToAsync($"{AppRoutes.EventDetail}?rehearsalDate={date}");
            }
            else
            {
                await _navigation.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventDto.Id}");
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít detail akce.";
        }
    }

    [RelayCommand]
    private async Task ShowNote(EventDto? eventDto)
    {
        // Called by TouchBehavior.LongPressCommand with the card's BindingContext.
        var comment = eventDto?.MyAttendance?.Comment;
        if (string.IsNullOrWhiteSpace(comment)) return;

        // Mark the long-press window so the CollectionView selection that fires
        // on finger-release doesn't also navigate to the event detail.
        Behaviors.LongPressTracker.LastFiredUtc = DateTime.UtcNow;

        try
        {
            await Shell.Current.DisplayAlert("Poznámka", comment, "OK");
        }
        catch
        {
            // Shell.Current can be null during transitions; swallow.
        }
    }

    [RelayCommand]
    private async Task NavigateToStats()
    {
        try
        {
            var from = new DateTime(CurrentYear, CurrentMonth, 1).ToString("yyyy-MM-dd");
            var to = new DateTime(CurrentYear, CurrentMonth, DateTime.DaysInMonth(CurrentYear, CurrentMonth)).ToString("yyyy-MM-dd");
            await _navigation.GoToAsync($"{AppRoutes.AttdStats}?from={from}&to={to}");
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
            await _navigation.GoToAsync($"{AppRoutes.AttdOverview}?year={CurrentYear}&month={CurrentMonth}");
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít přehled docházky.";
        }
    }
}