using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Attendances;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels.Attendance;

/// <summary>
/// Attendance statistics — shows rehearsal and action attendance rates
/// for all members in a configurable date range.
/// </summary>
public partial class AttendanceStatsViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;

    public AttendanceStatsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        _dateFrom = new DateTime(DateTime.Today.Year, 1, 1);
        _dateTo = DateTime.Today;
    }

    [ObservableProperty]
    private DateTime _dateFrom;

    [ObservableProperty]
    private DateTime _dateTo;

    public DateTime MaxDate => DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStats))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(TotalRehearsals))]
    [NotifyPropertyChangedFor(nameof(TotalActions))]
    private ObservableCollection<MemberAttendanceStatDto> _stats = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasStats => Stats.Count > 0;
    public bool IsEmpty => !IsBusy && Stats.Count == 0;
    public int TotalRehearsals => Stats.FirstOrDefault()?.TotalRehearsals ?? 0;
    public int TotalActions => Stats.FirstOrDefault()?.TotalActions ?? 0;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetAttendanceStatsAsync(DateFrom, DateTo);
            Stats = new ObservableCollection<MemberAttendanceStatDto>(result);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst statistiky.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
