using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;
using System.Collections.ObjectModel;

namespace Demizon.Maui.ViewModels;

public partial class EventsViewModel : ObservableObject, IRecipient<EventsChangedMessage>
{
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigation;

    public EventsViewModel(IApiClient apiClient, INavigationService navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
        WeakReferenceMessenger.Default.Register(this);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<EventDto> _events = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasEvents => Events.Count > 0;
    public bool IsEmpty => !IsBusy && Events.Count == 0;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.GetUpcomingEventsAsync();
            Events = new ObservableCollection<EventDto>(result);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst akce.";
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToDetail(EventDto eventDto)
    {
        await _navigation.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventDto.Id}");
    }

    [RelayCommand]
    private async Task NavigateToCreate()
    {
        await _navigation.GoToAsync(AppRoutes.EventCreate);
    }

    public void Receive(EventsChangedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() => LoadCommand.Execute(null));
    }
}
