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

        var pan = new PanGestureRecognizer();
        double startX = 0;
        double startY = 0;
        bool active = false;

        pan.PanUpdated += (_, e) =>
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    startX = e.TotalX;
                    startY = e.TotalY;
                    active = true;
                    break;

                case GestureStatus.Running when active:
                    double dx = e.TotalX - startX;
                    double dy = e.TotalY - startY;
                    if (Math.Abs(dx) > 60 && Math.Abs(dx) > Math.Abs(dy) * 2)
                    {
                        active = false;
                        if (dx < 0)
                            _vm?.NextMonthCommand.Execute(null);
                        else
                            _vm?.PreviousMonthCommand.Execute(null);
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    active = false;
                    break;
            }
        };

        ContentGrid.GestureRecognizers.Add(pan);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AttendanceViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
