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
    private readonly TokenStorage _tokenStorage;

    // Cached on first load so we can distinguish "my" row without repeated SecureStorage calls
    private int? _currentMemberId;

    public AllMembersAttendanceViewModel(IApiClient apiClient, INavigationService navigation, TokenStorage tokenStorage)
    {
        _apiClient = apiClient;
        _navigation = navigation;
        _tokenStorage = tokenStorage;
        var today = DateTime.Today;
        _currentYear = today.Year;
        _currentMonth = today.Month;
    }

    /// <summary>Whether the given member ID belongs to the currently logged-in user.</summary>
    public bool IsCurrentUser(int memberId) => _currentMemberId.HasValue && _currentMemberId.Value == memberId;

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
            // Cache the current user's memberId on first successful load
            if (_currentMemberId is null)
                _currentMemberId = await _tokenStorage.GetMemberIdAsync();

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
        try
        {
            await _navigation.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventId}");
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít detail akce.";
        }
    }

    public async Task NavigateToRehearsalAsync(DateTime date)
    {
        try
        {
            await _navigation.GoToAsync(
                $"{AppRoutes.EventDetail}?rehearsalDate={date:yyyy-MM-dd}");
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít detail zkoušky.";
        }
    }

    public async Task NavigateToMemberAttendanceAsync(int eventId, int memberId, string memberName)
    {
        try
        {
            var encodedName = Uri.EscapeDataString(memberName);
            await _navigation.GoToAsync(
                $"{AppRoutes.MemberAttdDetail}?eventId={eventId}&memberId={memberId}&memberName={encodedName}");
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se otevřít docházku člena.";
        }
    }
}
