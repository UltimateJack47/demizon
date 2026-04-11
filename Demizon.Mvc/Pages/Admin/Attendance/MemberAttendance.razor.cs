using System.Security.Claims;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.Member;
using Demizon.Dal.Entities;
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

    // Aktuálně zobrazený měsíc
    private DateTime CurrentMonth { get; set; }
    private DateTime StartDate => new(CurrentMonth.Year, CurrentMonth.Month, 1);
    private DateTime EndDate => StartDate.AddMonths(1).AddDays(-1);

    private MemberViewModel LoggedUser { get; set; } = new();

    // Sloupce tabulky (pátky + akce v měsíci)
    private List<AttendanceViewModel> Attendances { get; set; } = [];

    // Řádky tabulky (členové)
    private List<MemberViewModel> TableMembers { get; set; } = [];

    // Filtr pohlaví: null = vše, Male, Female
    private Gender? GenderFilter { get; set; } = null;

    // Zobrazit i skryté v docházce
    private bool ShowAttendanceHidden { get; set; } = false;

    private IEnumerable<MemberViewModel> FilteredFemales =>
        TableMembers
            .Where(x => x.Gender == Gender.Female)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible);

    private IEnumerable<MemberViewModel> FilteredMales =>
        TableMembers
            .Where(x => x.Gender == Gender.Male)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible);

    protected override async Task OnInitializedAsync()
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var authState = await AuthenticationState!;
        var loggedUserLogin = authState.User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
        LoggedUser = MemberService.GetOneByLogin(loggedUserLogin)!.ToViewModel();

        PageService.SetTitle(Localizer[nameof(DemizonLocales.Attendance)]);

        await LoadData();
        await LoadTableData();
    }

    private async Task LoadData()
    {
        // Pátky v aktuálním měsíci
        var allDates = Enumerable
            .Range(0, 1 + EndDate.Subtract(StartDate).Days)
            .Select(offset => StartDate.AddDays(offset))
            .ToList();

        var events = EventService.GetAll()
            .Where(x => x.DateFrom >= StartDate && x.DateFrom <= EndDate)
            .ToList()
            .Select(x => x.ToViewModel())
            .ToList();

        // Pátky bez akce
        Attendances = allDates
            .Where(date => date.DayOfWeek == DayOfWeek.Friday)
            .Where(date => !events.Any(e => e.DateFrom.Date == date.Date))
            .Select(x => new AttendanceViewModel
            {
                Member = LoggedUser,
                MemberId = LoggedUser.Id,
                Date = x,
            }).ToList();

        // Akce
        Attendances.AddRange(events.Select(x => new AttendanceViewModel
        {
            Event = x,
            EventId = x.Id,
            MemberId = LoggedUser.Id,
            Member = LoggedUser,
            Date = x.DateFrom
        }));

        // Načteme docházku přihlášeného uživatele
        LoggedUser.Attendances = (await AttendanceService.GetMemberAttendancesAsync(LoggedUser.Id, StartDate, EndDate))
            .Select(x => x.ToViewModel())
            .ToList();

        foreach (var attendance in Attendances)
        {
            var userAttendance = LoggedUser.Attendances.FirstOrDefault(x =>
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

    private async Task LoadTableData()
    {
        // Načteme všechny viditelné členy (IsVisible = zobrazuje se na webu)
        TableMembers = MemberService.GetAll()
            .Where(x => x.IsVisible)
            .ToList()
            .Select(x => x.ToViewModel())
            .ToList();

        var membersAttendances = (await AttendanceService.GetMembersAttendancesAsync(
                TableMembers.Select(x => x.Id).ToList(), StartDate, EndDate))
            .Select(x => x.ToViewModel())
            .ToList();

        foreach (var member in TableMembers)
        {
            member.Attendances = membersAttendances.Where(x => x.MemberId == member.Id).ToList();
        }
    }

    private string GetThemeStyle(AttendanceViewModel date)
    {
        var userAttendance = LoggedUser.Attendances.FirstOrDefault(x => x.Date == date.Date);
        if (userAttendance is null)
            return date.Event is not null ? "mud-theme-warning" : "mud-theme-info";

        return userAttendance.Attends ? "mud-theme-success" : "mud-theme-error";
    }

    private int CountAttending(IEnumerable<MemberViewModel> members, DateTime date) =>
        members.SelectMany(x => x.Attendances)
            .Where(y => y.Date == date)
            .Count(y => y.Attends);

    private async Task SetAttendance(AttendanceViewModel attendance)
    {
        await SaveAttendanceAsync(attendance, refreshUserData: true);
    }

    private async Task PreviousMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        await LoadData();
        await LoadTableData();
    }

    private async Task NextMonth()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        await LoadData();
        await LoadTableData();
    }

    private string MonthLabel => CurrentMonth.ToString("MMMM yyyy");

    // Kliknutí na buňku konkrétního člena v tabulce (admin může editovat kohokoliv)
    private async Task SetAttendanceMember(MemberViewModel member, AttendanceViewModel col)
    {
        var existing = member.Attendances.FirstOrDefault(x =>
            x.Date == col.Date || (col.EventId.HasValue && x.EventId == col.EventId));

        var model = existing ?? new AttendanceViewModel
        {
            MemberId = member.Id,
            Member = member,
            Date = col.Date,
            EventId = col.EventId,
            Event = col.Event,
        };
        if (existing is not null)
        {
            model.Event = col.Event;
        }

        await SaveAttendanceAsync(model, refreshUserData: false);
    }

    private async Task SaveAttendanceAsync(AttendanceViewModel model, bool refreshUserData)
    {
        var options = new DialogOptions { CloseOnEscapeKey = true };
        var parameters = new DialogParameters { { "Model", model } };
        var dialog = await DialogService.ShowAsync<AttendanceForm>(null, parameters, options);
        var result = await dialog.Result;

        if (result!.Canceled) return;

        try
        {
            var attendanceResult = result.Data as AttendanceViewModel;
            await AttendanceService.CreateOrUpdateAsync(attendanceResult!.ToEntity());
            if (refreshUserData) await LoadData();
            await LoadTableData();
            Snackbar.Add("Docházka uložena.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Chyba při ukládání.", Severity.Error);
        }
    }
}
