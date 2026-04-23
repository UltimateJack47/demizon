using Demizon.Maui.ViewModels.Attendance;

namespace Demizon.Maui.Pages.Attendance;

public partial class AttendancePage : ContentPage
{
    private AttendanceViewModel? _vm;

    public AttendancePage(AttendanceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _vm = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AttendanceViewModel vm)
            vm.LoadCommand.Execute(null);

#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var decor = activity?.Window?.DecorView;
        if (decor is not null)
        {
            decor.Post(() => AppShell.FindAndHideToolbar(decor));
            decor.PostDelayed(() => AppShell.FindAndHideToolbar(decor), 500);
        }

        if (activity is Demizon.Maui.Platforms.Android.MainActivity && activity is not null)
        {
            Demizon.Maui.Platforms.Android.MainActivity.CurrentSwipeInterceptor =
                new Demizon.Maui.Platforms.Android.SwipeGestureInterceptor(
                    activity,
                    onSwipeLeft:  () => MainThread.BeginInvokeOnMainThread(() => _vm?.NextMonthCommand.Execute(null)),
                    onSwipeRight: () => MainThread.BeginInvokeOnMainThread(() => _vm?.PreviousMonthCommand.Execute(null)));
        }
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        Demizon.Maui.Platforms.Android.MainActivity.CurrentSwipeInterceptor = null;
#endif
    }
}
