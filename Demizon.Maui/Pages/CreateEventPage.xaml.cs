using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class CreateEventPage : ContentPage
{
    public CreateEventPage(CreateEventViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
