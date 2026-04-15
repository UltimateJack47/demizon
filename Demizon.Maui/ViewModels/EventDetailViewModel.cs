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
    private bool _attends;

    [ObservableProperty]
    private string? _comment;

    [ObservableProperty]
    private string? _activityRole;

    [ObservableProperty]
    private bool _isBusy;

    public List<string> RoleOptions { get; } = ["Tanečník", "Muzikant"];

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
                try
                {
                    var att = await apiClient.GetRehearsalAttendanceAsync(date);
                    Attends = att.Attends;
                    Comment = att.Comment;
                    ActivityRole = null;
                }
                catch
                {
                    // No existing attendance record — default to not attending
                    Attends = false;
                    Comment = null;
                    ActivityRole = null;
                }
            }
            else
            {
                Event = await apiClient.GetEventAsync(EventId);
                if (Event?.MyAttendance is { } att)
                {
                    Attends = att.Attends;
                    Comment = att.Comment;
                    ActivityRole = att.ActivityRole;
                }
                else
                {
                    Attends = false;
                    Comment = null;
                    ActivityRole = null;
                }
            }
        }
        catch (Exception)
        {
            // Silently ignore load errors — page will show empty state
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SetAttends(bool value)
    {
        Attends = value;
    }

    [RelayCommand]
    public async Task SaveAttendanceAsync()
    {
        IsBusy = true;
        try
        {
            var request = new UpsertAttendanceRequest(Attends, Comment, ActivityRole);
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
