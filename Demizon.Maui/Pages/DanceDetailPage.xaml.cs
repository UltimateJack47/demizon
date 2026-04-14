using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class DanceDetailPage : ContentPage
{
    public DanceDetailPage(DanceDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DanceDetailViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
