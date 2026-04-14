using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class DancesPage : ContentPage
{
    public DancesPage(DancesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DancesViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
