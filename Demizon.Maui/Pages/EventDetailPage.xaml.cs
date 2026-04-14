using Demizon.Maui.ViewModels;

namespace Demizon.Maui.Pages;

public partial class EventDetailPage : ContentPage
{
    public EventDetailPage(EventDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EventDetailViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
