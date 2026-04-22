using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

[QueryProperty(nameof(EventId), "eventId")]
[QueryProperty(nameof(RehearsalDateString), "rehearsalDate")]
public partial class EventDetailViewModel(IApiClient apiClient, INavigationService navigation, TokenStorage tokenStorage) : ObservableObject
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
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = "no";

    public bool IsAttending => Status == "yes";

    public bool HasStatus => !string.IsNullOrEmpty(Status);

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

    [ObservableProperty]
    private bool _isAdmin;

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
            var role = await tokenStorage.GetRoleAsync();
            IsAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && !IsRehearsal;

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
            if (string.IsNullOrEmpty(Status))
            {
                // Reset: delete attendance record
                if (IsRehearsal)
                {
                    var date = DateTime.Parse(RehearsalDateString!);
                    await apiClient.DeleteMyRehearsalAttendanceAsync(date);
                }
                else
                {
                    await apiClient.DeleteMyAttendanceAsync(EventId);
                }
            }
            else
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
            }
            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await navigation.GoBackAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task EditEventAsync()
    {
        await navigation.GoToAsync(AppRoutes.EditEvent,
            new Dictionary<string, object> { ["eventId"] = EventId });
    }

    [RelayCommand]
    private async Task DeleteEventAsync()
    {
        var confirm = await Application.Current!.MainPage!
            .DisplayAlert("Smazat akci", "Opravdu chcete smazat tuto akci?", "Smazat", "Zrušit");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            await apiClient.DeleteEventAsync(EventId);
            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await navigation.GoBackAsync();
        }
        catch (Exception) { /* ignore */ }
        finally { IsBusy = false; }
    }
}
