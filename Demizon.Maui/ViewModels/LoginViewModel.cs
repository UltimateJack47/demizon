using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Auth;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

public partial class LoginViewModel(IApiClient apiClient, TokenStorage tokenStorage) : ObservableObject
{
    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var response = await apiClient.LoginAsync(new TokenRequest(Login, Password));
            await tokenStorage.SaveAsync(response.Token, response.RefreshToken);
            await Shell.Current.GoToAsync("//events");
        }
        catch (Exception)
        {
            ErrorMessage = "Přihlášení selhalo. Zkontrolujte přihlašovací údaje.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
