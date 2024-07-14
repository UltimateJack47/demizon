using System.Security.Claims;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Member;
using Demizon.Mvc.Locales;
using Demizon.Mvc.Pages.Admin.Attendance.Components;
using Demizon.Mvc.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace Demizon.Mvc.Pages.Admin.Attendance;

public partial class MemberAttendance : ComponentBase
{
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    [Inject] private IMemberService MemberService { get; set; } = null!;
    [Inject] private IAttendanceService AttendanceService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private MemberViewModel LoggedUser { get; set; } = new();

    private List<DateTime> Dates { get; set; } = [];

    protected override void OnInitialized()
    {
        var loggedUserLogin = AuthenticationState?.Result.User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
        LoggedUser = Mapper.Map<MemberViewModel>(MemberService.GetOneByLogin(loggedUserLogin));

        var startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month + 3, 1);
        var allDates = Enumerable.Range(0, 1 + endDate.Subtract(startDate).Days)
            .Select(offset => startDate.AddDays(offset))
            .ToList();

        Dates = allDates.Where(date => date.DayOfWeek == DayOfWeek.Friday).ToList();
        PageService.SetTitle(Localizer[nameof(DemizonLocales.Attendance)]);
    }

    protected override async Task OnInitializedAsync()
    {
        LoggedUser.Attendances =
            Mapper.Map<List<AttendanceViewModel>>(await AttendanceService.GetMemberAttendancesAsync(LoggedUser.Id));
    }

    private string GetThemeStyle(DateTime date)
    {
        var attendance = LoggedUser.Attendances.FirstOrDefault(x => x.Date == date);
        if (attendance is null)
        {
            return "pa-3 mud-theme-info";
        }

        return attendance.Attends switch
        {
            true => "pa-3 mud-theme-success",
            false => "pa-3 mud-theme-error"
        };
    }

    private async Task SetAttendance(DateTime date)
    {
        var viewModel = LoggedUser.Attendances.FirstOrDefault(x => x.Date == date);
        var isUpdate = viewModel is not null;
        var options = new DialogOptions {CloseOnEscapeKey = true};
        var parameters = new DialogParameters
        {
            {"IsUpdate", isUpdate},
            {"Model", viewModel ?? new AttendanceViewModel()},
            {"Date", date}
        };
        var dialog = await DialogService.ShowAsync<AttendanceForm>("Attendance form", parameters, options);

        var result = await dialog.Result;

        if (!result.Canceled)
        {
            var attendanceResult = result.Data as AttendanceViewModel;
            try
            {
                attendanceResult!.Date = date;
                attendanceResult.MemberId = LoggedUser.Id;
                if (isUpdate)
                {
                    await AttendanceService.UpdateAsync(viewModel!.Id,
                        Mapper.Map<Dal.Entities.Attendance>(attendanceResult));
                    Snackbar.Add("The attendance has been updated.", Severity.Success);
                }
                else
                {
                    await AttendanceService.CreateAsync(Mapper.Map<Dal.Entities.Attendance>(attendanceResult));
                    Snackbar.Add("The attendance has been created.", Severity.Success);
                }

                StateHasChanged();
            }
            catch (Exception)
            {
                Snackbar.Add("Something went wrong.", Severity.Error);
            }
        }
    }
}
