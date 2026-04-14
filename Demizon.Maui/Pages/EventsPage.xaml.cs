using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class EventsPage : ContentPage
{
    public EventsPage(EventsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EventsViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
