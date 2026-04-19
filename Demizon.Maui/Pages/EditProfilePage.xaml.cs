using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage(EditProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EditProfileViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
