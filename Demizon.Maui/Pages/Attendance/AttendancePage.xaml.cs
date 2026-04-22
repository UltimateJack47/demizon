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

        var swipeLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left, Threshold = 40 };
        swipeLeft.Swiped += (_, _) => _vm?.NextMonthCommand.Execute(null);

        var swipeRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right, Threshold = 40 };
        swipeRight.Swiped += (_, _) => _vm?.PreviousMonthCommand.Execute(null);

        // Attach swipe gestures to the page's root content element
        if (Content is View rootView)
        {
            rootView.GestureRecognizers.Add(swipeLeft);
            rootView.GestureRecognizers.Add(swipeRight);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AttendanceViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
