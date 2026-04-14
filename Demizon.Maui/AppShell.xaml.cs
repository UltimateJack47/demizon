namespace Demizon.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("events/detail", typeof(Pages.EventDetailPage));
        Routing.RegisterRoute("events/create", typeof(Pages.CreateEventPage));
        Routing.RegisterRoute("dances/detail", typeof(Pages.DanceDetailPage));
    }
}
