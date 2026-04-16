using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels.Attendance;

/// <summary>
/// Admin view: edit another member's attendance for a specific event.
/// </summary>
[QueryProperty(nameof(EventId), "eventId")]
[QueryProperty(nameof(MemberId), "memberId")]
[QueryProperty(nameof(MemberName), "memberName")]
public partial class MemberAttendanceDetailViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private int _eventId;

    [ObservableProperty]
    private int _memberId;

    [ObservableProperty]
    private string? _memberName;

    [ObservableProperty]
    private EventDto? _event;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAttending))]
    private string _status = "no";

    public bool IsAttending => Status == "yes";

    [ObservableProperty]
    private string? _comment;

    [ObservableProperty]
    private string? _activityRole;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    // Display labels for the Picker (Czech). Conversion to/from API values (English) happens at load/save.
    public List<string> RoleOptions { get; } = ["Tanečník", "Muzikant"];

    private static string? ApiRoleToDisplay(string? apiRole) => apiRole switch
    {
        "dancer" => "Tanečník",
        "musician" => "Muzikant",
        _ => null
    };

    private static string? DisplayRoleToApi(string? displayRole) => displayRole switch
    {
        "Tanečník" => "dancer",
        "Muzikant" => "musician",
        _ => null
    };

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (EventId == 0 || MemberId == 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var (eventTask, attendanceTask) = (
                apiClient.GetEventAsync(EventId),
                apiClient.GetMemberAttendanceAsync(EventId, MemberId)
            );
            await Task.WhenAll(eventTask, attendanceTask);

            Event = await eventTask;

            var att = await attendanceTask;
            Status = att.Status;
            Comment = att.Comment;
            ActivityRole = ApiRoleToDisplay(att.ActivityRole);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst docházku.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetStatus(string value) => Status = value;

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (EventId == 0 || MemberId == 0) return;

        IsBusy = true;
        try
        {
            var request = new UpsertAttendanceRequest(Status, Comment, DisplayRoleToApi(ActivityRole));
            await apiClient.UpsertMemberAttendanceAsync(EventId, MemberId, request);
            await navigation.GoBackAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se uložit docházku.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
