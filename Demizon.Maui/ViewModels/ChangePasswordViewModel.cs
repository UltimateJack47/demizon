using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Members;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

public partial class ChangePasswordViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [RelayCommand]
    public async Task ChangeAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "Zadejte aktuální heslo.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Zadejte nové heslo.";
            return;
        }

        if (NewPassword.Length < 4)
        {
            ErrorMessage = "Nové heslo musí mít alespoň 4 znaky.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Nová hesla se neshodují.";
            return;
        }

        IsBusy = true;
        try
        {
            var request = new ChangePasswordRequest(CurrentPassword, NewPassword);
            await apiClient.ChangePasswordAsync(request);
            SuccessMessage = "Heslo bylo úspěšně změněno.";

            await Task.Delay(1500);
            await navigation.GoBackAsync();
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            ErrorMessage = "Aktuální heslo je nesprávné.";
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se změnit heslo.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
