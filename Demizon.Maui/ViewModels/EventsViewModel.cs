using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Events;
using Demizon.Maui.Services;
using System.Collections.ObjectModel;

namespace Demizon.Maui.ViewModels;

public partial class EventsViewModel(IApiClient apiClient) : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<EventDto> _events = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await apiClient.GetUpcomingEventsAsync();
            Events = new ObservableCollection<EventDto>(result);
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst akce.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
