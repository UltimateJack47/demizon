using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Attendances;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels.Attendance;

/// <summary>
/// Monthly overview of all members' attendance — shows every member × every event/rehearsal column.
/// </summary>
public partial class AllMembersAttendanceViewModel : ObservableObject
{
    private static readonly CultureInfo CsCulture = new("cs-CZ");

    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigation;

    public AllMembersAttendanceViewModel(IApiClient apiClient, INavigationService navigation)
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
    [NotifyPropertyChangedFor(nameof(HasData))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private MonthlyAttendanceTableDto? _table;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public string MonthLabel => new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM yyyy", CsCulture);
    public bool HasData => Table is not null && Table.Members.Count > 0;
    public bool IsEmpty => !IsBusy && !HasData;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            Table = await _apiClient.GetMonthlyAttendanceTableAsync(CurrentYear, CurrentMonth);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst přehled docházky.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousMonth()
    {
        if (CurrentMonth == 1) { CurrentMonth = 12; CurrentYear--; }
        else CurrentMonth--;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonth()
    {
        if (CurrentMonth == 12) { CurrentMonth = 1; CurrentYear++; }
        else CurrentMonth++;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateToEvent(int eventId)
    {
        await _navigation.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventId}");
    }
}
