using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Dances;
using Demizon.Maui.Services;
using System.Collections.ObjectModel;

namespace Demizon.Maui.ViewModels;

public partial class DancesViewModel(IApiClient apiClient) : ObservableObject
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
        await Shell.Current.GoToAsync($"//dances/detail?danceId={dance.Id}");
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
