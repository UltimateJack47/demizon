using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        LoadLogo();
    }

    private async void LoadLogo()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("demizon_logo.jpg");
            LogoImage.Source = ImageSource.FromStream(() =>
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            });
        }
        catch
        {
            // Fallback: logo won't show but app remains functional
        }
    }
}
