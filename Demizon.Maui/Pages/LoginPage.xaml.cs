using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            using var fileStream = await FileSystem.OpenAppPackageFileAsync("demizon_logo.jpg");
            using var memStream = new MemoryStream();
            await fileStream.CopyToAsync(memStream);
            var bytes = memStream.ToArray();
            LogoImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch
        {
            // Logo not critical — silently skip if asset missing
        }
    }
}
