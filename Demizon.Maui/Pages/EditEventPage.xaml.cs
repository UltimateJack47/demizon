using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class EditEventPage : ContentPage
{
    public EditEventPage(EditEventViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EditEventViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
