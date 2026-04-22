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
        Routing.RegisterRoute(AppRoutes.MemberAttdDetail, typeof(Pages.Attendance.MemberAttendanceDetailPage));
        Routing.RegisterRoute(AppRoutes.EditProfile, typeof(Pages.EditProfilePage));
        Routing.RegisterRoute(AppRoutes.ChangePassword, typeof(Pages.ChangePasswordPage));
        Routing.RegisterRoute(AppRoutes.EditEvent, typeof(Pages.EditEventPage));
        Routing.RegisterRoute(AppRoutes.Gallery, typeof(Pages.GalleryPage));
        Routing.RegisterRoute(AppRoutes.PhotoViewer, typeof(Pages.PhotoViewerPage));
    }
}
