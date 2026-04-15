using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Auth;
using Demizon.Maui.Services;
using Refit;

namespace Demizon.Maui.ViewModels;

public partial class LoginViewModel(IApiClient apiClient, TokenStorage tokenStorage, INavigationService navigation) : ObservableObject
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
            await tokenStorage.SaveAsync(response, Login);
            await navigation.GoToAsync(AppRoutes.MainTabs);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Nesprávné přihlašovací jméno nebo heslo.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Nelze se připojit k serveru. Zkontrolujte síťové připojení.";
        }
        catch (Exception)
        {
            ErrorMessage = "Přihlášení selhalo. Zkuste to prosím znovu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
