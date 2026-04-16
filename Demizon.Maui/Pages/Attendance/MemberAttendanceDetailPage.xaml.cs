using Demizon.Maui.ViewModels.Attendance;

namespace Demizon.Maui.Pages.Attendance;

public partial class MemberAttendanceDetailPage : ContentPage
{
    public MemberAttendanceDetailPage(MemberAttendanceDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MemberAttendanceDetailViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
