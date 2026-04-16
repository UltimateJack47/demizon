using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

[QueryProperty(nameof(EventId), "eventId")]
[QueryProperty(nameof(RehearsalDateString), "rehearsalDate")]
public partial class EventDetailViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private int _eventId;

    // Set when editing a rehearsal (no EventId). Format: "yyyy-MM-dd"
    [ObservableProperty]
    private string? _rehearsalDateString;

    private bool IsRehearsal => EventId == 0 && !string.IsNullOrEmpty(RehearsalDateString);

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
    [NotifyPropertyChangedFor(nameof(ShowAttendees))]
    private EventAttendeesDto? _attendees;

    public bool ShowAttendees => !IsRehearsal && Attendees?.TotalCount > 0;

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
        IsBusy = true;
        try
        {
            if (IsRehearsal)
            {
                var date = DateTime.Parse(RehearsalDateString!);
                Event = new EventDto(0, "Zkouška", date, date.AddHours(2), null, false, "Weekly", IsRehearsal: true);
                Attendees = null;
                try
                {
                    var att = await apiClient.GetRehearsalAttendanceAsync(date);
                    Status = att.Status;
                    Comment = att.Comment;
                    ActivityRole = ApiRoleToDisplay(att.ActivityRole);
                }
                catch
                {
                    // No existing attendance record — default to not attending
                    Status = "no";
                    Comment = null;
                    ActivityRole = null;
                }
            }
            else
            {
                Event = await apiClient.GetEventAsync(EventId);
                if (Event?.MyAttendance is { } att)
                {
                    Status = att.Status;
                    Comment = att.Comment;
                    ActivityRole = ApiRoleToDisplay(att.ActivityRole);
                }
                else
                {
                    Status = "no";
                    Comment = null;
                    ActivityRole = null;
                }

                // Load attendees list
                try { Attendees = await apiClient.GetEventAttendeesAsync(EventId); }
                catch { Attendees = null; }
            }
        }
        catch (Exception)
        {
            // Silently ignore load errors — page will show empty state
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SetStatus(string value)
    {
        Status = value;
    }

    [RelayCommand]
    public async Task SaveAttendanceAsync()
    {
        IsBusy = true;
        try
        {
            var request = new UpsertAttendanceRequest(Status, Comment, DisplayRoleToApi(ActivityRole));
            if (IsRehearsal)
            {
                var date = DateTime.Parse(RehearsalDateString!);
                await apiClient.UpsertRehearsalAttendanceAsync(date, request);
            }
            else
            {
                await apiClient.UpsertAttendanceAsync(EventId, request);
            }
            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await navigation.GoBackAsync();
        }
        finally { IsBusy = false; }
    }
}
