using System.Security.Claims;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
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
    [Inject] private IEventService EventService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private int Year { get; set; }
    private int Month { get; set; }
    private DateTime StartDate { get; set; }

    private MemberViewModel LoggedUser { get; set; } = new();

    private List<AttendanceViewModel> Attendances { get; set; } = [];

    protected override void OnInitialized()
    {
        if (Year == 0)
        {
            Year = DateTime.Today.Year;
        }

        if (Month == 0)
        {
            Month = DateTime.Today.Month;
        }

        var loggedUserLogin = AuthenticationState?.Result.User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
        LoggedUser = Mapper.Map<MemberViewModel>(MemberService.GetOneByLogin(loggedUserLogin));

        PageService.SetTitle(Localizer[nameof(DemizonLocales.Attendance)]);
    }

    private async Task LoadData()
    {
        StartDate = new DateTime(Year, Month, 1);
        var endDate = new DateTime(StartDate.AddMonths(3).Year, StartDate.AddMonths(3).Month, 1);
        var allDates = Enumerable.Range(0, 1 + endDate.Subtract(StartDate).Days)
            .Select(offset => StartDate.AddDays(offset))
            .ToList();

        var events = Mapper.Map<List<EventViewModel>>(EventService.GetAll()
            .Where(x => x.DateFrom >= StartDate && x.DateFrom <= endDate).ToList());
        Attendances = allDates.Where(date => date.DayOfWeek == DayOfWeek.Friday).Select(x => new AttendanceViewModel
        {
            Member = LoggedUser,
            MemberId = LoggedUser.Id,
            Date = x,
        }).ToList();
        Attendances.AddRange(events.Select(x => new AttendanceViewModel
        {
            Event = x,
            EventId = x.Id,
            MemberId = LoggedUser.Id,
            Member = LoggedUser,
            Date = x.DateFrom
        }));
        LoggedUser.Attendances =
            Mapper.Map<List<AttendanceViewModel>>(
                await AttendanceService.GetMemberAttendancesAsync(LoggedUser.Id, StartDate, endDate));
        foreach (var attendance in Attendances)
        {
            var userAttendance =
                LoggedUser.Attendances.FirstOrDefault(x =>
                    x.Date == attendance.Date || (x.EventId.HasValue && x.EventId == attendance.EventId));
            if (userAttendance is not null)
            {
                attendance.Id = userAttendance.Id;
                attendance.Attends = userAttendance.Attends;
                attendance.Comment = userAttendance.Comment;
            }
        }

        Attendances = Attendances.OrderBy(x => x.Date).ToList();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private string GetThemeStyle(AttendanceViewModel date)
    {
        var userAttendance = LoggedUser.Attendances.FirstOrDefault(x => x.Date == date.Date);
        if (userAttendance is null)
        {
            return date.Event is not null ? "pa-3 mud-theme-warning" : "pa-3 mud-theme-info";
        }

        return userAttendance.Attends switch
        {
            true => "pa-3 mud-theme-success",
            false => "pa-3 mud-theme-error"
        };
    }

    private async Task SetAttendance(AttendanceViewModel attendance)
    {
        var options = new DialogOptions {CloseOnEscapeKey = true};
        var parameters = new DialogParameters
        {
            {"Model", attendance}
        };
        var dialog = await DialogService.ShowAsync<AttendanceForm>("Attendance form", parameters, options);

        var result = await dialog.Result;

        if (!result.Canceled)
        {
            var attendanceResult = result.Data as AttendanceViewModel;
            try
            {
                await AttendanceService.CreateOrUpdateAsync(Mapper.Map<Dal.Entities.Attendance>(attendanceResult));
                await LoadData();
                Snackbar.Add("The attendance has been updated.", Severity.Success);
            }
            catch (Exception)
            {
                Snackbar.Add("Something went wrong.", Severity.Error);
            }
        }
    }

    private async Task ChangeDate(bool isNext)
    {
        StartDate = isNext ? StartDate.AddMonths(3) : StartDate.AddMonths(-3);
        Year = StartDate.Year;
        Month = StartDate.Month;
        await LoadData();
    }
}
