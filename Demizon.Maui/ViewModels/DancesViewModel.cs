using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Dances;
using Demizon.Maui.Services;
using System.Collections.ObjectModel;

namespace Demizon.Maui.ViewModels;

public partial class DancesViewModel(IApiClient apiClient) : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DanceDto> _dances = [];

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await apiClient.GetDancesAsync();
            Dances = new ObservableCollection<DanceDto>(result);
        }
        finally { IsBusy = false; }
    }
}
