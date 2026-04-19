using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Members;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

public partial class EditProfileViewModel(IApiClient apiClient, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _surname = string.Empty;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var profile = await apiClient.GetMyProfileAsync();
            Name = profile.Name;
            Surname = profile.Surname;
            Email = profile.Email;
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se načíst profil.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Jméno je povinné.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Surname))
        {
            ErrorMessage = "Příjmení je povinné.";
            return;
        }

        IsBusy = true;
        try
        {
            var request = new UpdateProfileRequest(Name.Trim(), Surname.Trim(),
                string.IsNullOrWhiteSpace(Email) ? null : Email.Trim());
            await apiClient.UpdateMyProfileAsync(request);
            await navigation.GoBackAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Nepodařilo se uložit profil.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
