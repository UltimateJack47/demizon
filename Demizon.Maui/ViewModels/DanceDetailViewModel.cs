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

    [ObservableProperty]
    private bool _isLyricsExpanded;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try { Dance = await apiClient.GetDanceAsync(DanceId); }
        catch (Exception) { ErrorMessage = "Nepodařilo se načíst tanec."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ToggleLyrics() => IsLyricsExpanded = !IsLyricsExpanded;

    [RelayCommand]
    private async Task OpenVideoAsync(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            try
            {
                await Launcher.OpenAsync(uri);
            }
            catch
            {
                await Shell.Current.DisplayAlert("Chyba", "Nepodařilo se otevřít video.", "OK");
            }
        }
        else
        {
            await Shell.Current.DisplayAlert("Chyba", "Neplatný odkaz na video.", "OK");
        }
    }
}
