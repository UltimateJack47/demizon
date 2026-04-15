using Demizon.Maui.ViewModels.Attendance;

namespace Demizon.Maui.Pages.Attendance;

public partial class AttendanceStatsPage : ContentPage
{
    public AttendanceStatsPage(AttendanceStatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AttendanceStatsViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
