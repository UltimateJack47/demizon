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

#if ANDROID
        HandlerChanged += OnHandlerChanged;
#endif
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
#endif
    }

#if ANDROID
    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (Handler?.PlatformView is Android.Views.View nativeView)
            AttachSwipeListener(nativeView);
    }

    private void AttachSwipeListener(Android.Views.View nativeView)
    {
        var detector = new Android.Views.GestureDetector(
            nativeView.Context,
            new SwipeListener(
                () => _vm?.NextMonthCommand.Execute(null),
                () => _vm?.PreviousMonthCommand.Execute(null)));

        nativeView.Touch += (_, args) =>
        {
            detector.OnTouchEvent(args.Event);
            args.Handled = false; // Don't consume
        };
    }

    private class SwipeListener(Action onSwipeLeft, Action onSwipeRight)
        : Android.Views.GestureDetector.SimpleOnGestureListener
    {
        private const int SwipeThreshold = 80;
        private const int SwipeVelocityThreshold = 100;

        public override bool OnDown(Android.Views.MotionEvent? e) => true;

        public override bool OnFling(Android.Views.MotionEvent? e1, Android.Views.MotionEvent? e2, float velocityX, float velocityY)
        {
            if (e1 is null || e2 is null) return false;

            float dx = e2.GetX() - e1.GetX();
            float dy = e2.GetY() - e1.GetY();

            if (Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dx) > SwipeThreshold && Math.Abs(velocityX) > SwipeVelocityThreshold)
            {
                if (dx < 0)
                    MainThread.BeginInvokeOnMainThread(onSwipeLeft);
                else
                    MainThread.BeginInvokeOnMainThread(onSwipeRight);
                return true;
            }
            return false;
        }
    }
#endif
}
