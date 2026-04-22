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

        Navigated += OnNavigated;
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (CurrentPage is not null)
            SetNavBarIsVisible(CurrentPage, false);

#if ANDROID
        HideAndroidToolbar();
#endif
    }

#if ANDROID
    private void HideAndroidToolbar()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var decor = activity?.Window?.DecorView;
        if (decor is null) return;

        // Post immediately + with delay to catch toolbar even when recreated during navigation
        decor.Post(() => FindAndHideToolbar(decor));
        decor.PostDelayed(() => FindAndHideToolbar(decor), 100);
        decor.PostDelayed(() => FindAndHideToolbar(decor), 300);
    }

    private static void FindAndHideToolbar(Android.Views.View? view)
    {
        if (view is AndroidX.AppCompat.Widget.Toolbar toolbar)
        {
            toolbar.Visibility = Android.Views.ViewStates.Gone;
            // Also collapse the toolbar so its parent layout doesn't reserve space
            if (toolbar.LayoutParameters is { } lp)
            {
                lp.Height = 0;
                toolbar.LayoutParameters = lp;
            }
            return;
        }
        if (view is Android.Views.ViewGroup vg)
        {
            for (int i = 0; i < vg.ChildCount; i++)
                FindAndHideToolbar(vg.GetChildAt(i));
        }
    }
#endif
}
