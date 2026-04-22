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

        var swipeLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
        swipeLeft.Swiped += (_, _) => _vm?.NextMonthCommand.Execute(null);

        var swipeRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        swipeRight.Swiped += (_, _) => _vm?.PreviousMonthCommand.Execute(null);

        RootGrid.GestureRecognizers.Add(swipeLeft);
        RootGrid.GestureRecognizers.Add(swipeRight);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AttendanceViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
