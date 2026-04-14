using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Dances;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

[QueryProperty(nameof(DanceId), "danceId")]
public partial class DanceDetailViewModel(IApiClient apiClient) : ObservableObject
{
    [ObservableProperty]
    private int _danceId;

    [ObservableProperty]
    private DanceDto? _dance;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try { Dance = await apiClient.GetDanceAsync(DanceId); }
        finally { IsBusy = false; }
    }
}
