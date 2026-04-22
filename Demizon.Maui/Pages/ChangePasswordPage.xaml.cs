using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
