using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels.Attendance;

/// <summary>
/// Admin view: edit another member's attendance for a specific event or rehearsal.
/// </summary>
[QueryProperty(nameof(EventId), "eventId")]
[QueryProperty(nameof(MemberId), "memberId")]
[QueryProperty(nameof(MemberName), "memberName")]
[QueryProperty(nameof(RehearsalDateString), "rehearsalDate")]
public partial class MemberAttendanceDetailViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private int _eventId;

    [ObservableProperty]
    private int _memberId;

    [ObservableProperty]
    private string? _memberName;

    [ObservableProperty]
    private string? _rehearsalDateString;

    private bool IsRehearsal => EventId == 0 && !string.IsNullOrEmpty(RehearsalDateString);

    [ObservableProperty]
    private EventDto? _event;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAttending))]
    [NotifyPropertyChangedFor(nameof(ShowRolePicker))]
    private string _status = "no";

    public bool IsAttending => Status == "yes";
    public bool ShowRolePicker => IsAttending && !IsRehearsal;

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
        if (MemberId == 0) return;
        if (EventId == 0 && !IsRehearsal) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsRehearsal)
            {
                var date = DateTime.Parse(RehearsalDateString!);
                Event = new EventDto(0, "Zkouška", date, date.AddHours(2), null, false, "Weekly", IsRehearsal: true);
                try
                {
                    var att = await apiClient.GetMemberRehearsalAttendanceAsync(MemberId, date);
                    Status = att.Status;
                    Comment = att.Comment;
                    ActivityRole = null;
                }
                catch
                {
                    Status = "no";
                    Comment = null;
                    ActivityRole = null;
                }
            }
            else
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
        if (MemberId == 0) return;
        if (EventId == 0 && !IsRehearsal) return;

        IsBusy = true;
        try
        {
            if (IsRehearsal)
            {
                var date = DateTime.Parse(RehearsalDateString!);
                var request = new UpsertAttendanceRequest(Status, Comment, null);
                await apiClient.UpsertMemberRehearsalAttendanceAsync(MemberId, date, request);
            }
            else
            {
                var request = new UpsertAttendanceRequest(Status, Comment, DisplayRoleToApi(ActivityRole));
                await apiClient.UpsertMemberAttendanceAsync(EventId, MemberId, request);
            }
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
