using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Dances;
using Demizon.Maui.Services;
using System.Collections.ObjectModel;

namespace Demizon.Maui.ViewModels;

public partial class DancesViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    private List<DanceDto> _allDances = [];

    [ObservableProperty]
    private ObservableCollection<DanceDto> _filteredDances = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _searchText;

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _allDances = await apiClient.GetDancesAsync();
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task NavigateToDetailAsync(DanceDto dance)
    {
        try
        {
            await navigation.GoToAsync($"{AppRoutes.DanceDetail}?danceId={dance.Id}");
        }
        catch (Exception)
        {
            // Navigation failures are non-critical; user can retry by tapping again
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(search)
            ? _allDances
            : _allDances.Where(d =>
                (d.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.Region?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
              .ToList();
        FilteredDances = new ObservableCollection<DanceDto>(filtered);
    }
}
