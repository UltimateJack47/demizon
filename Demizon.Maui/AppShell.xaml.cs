namespace Demizon.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(AppRoutes.AttdStats, typeof(Pages.Attendance.AttendanceStatsPage));
        Routing.RegisterRoute(AppRoutes.AttdOverview, typeof(Pages.Attendance.AllMembersAttendancePage));
        Routing.RegisterRoute(AppRoutes.EventDetail, typeof(Pages.EventDetailPage));
        Routing.RegisterRoute(AppRoutes.EventCreate, typeof(Pages.CreateEventPage));
        Routing.RegisterRoute(AppRoutes.DanceDetail, typeof(Pages.DanceDetailPage));
    }
}
