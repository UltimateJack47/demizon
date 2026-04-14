using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

[QueryProperty(nameof(EventId), "eventId")]
public partial class EventDetailViewModel(IApiClient apiClient) : ObservableObject
{
    [ObservableProperty]
    private int _eventId;

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
            await apiClient.UpsertAttendanceAsync(EventId, request);
            WeakReferenceMessenger.Default.Send(new EventsChangedMessage());
            await Shell.Current.GoToAsync("..");
        }
        finally { IsBusy = false; }
    }
}
