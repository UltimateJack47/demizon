using System.Globalization;
using System.Security.Claims;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Dal.Entities;
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
    [Inject] private IGoogleCalendarService GoogleCalendarService { get; set; } = null!;

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
            .Where(x => !x.IsExternal)
            .Where(x => x.Gender == Gender.Female)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible);

    private IEnumerable<MemberViewModel> FilteredMales =>
        TableMembers
            .Where(x => !x.IsExternal)
            .Where(x => x.Gender == Gender.Male)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible);

    private IEnumerable<MemberViewModel> FilteredExternals =>
        TableMembers
            .Where(x => x.IsExternal)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible);

    protected override async Task OnInitializedAsync()
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var authState = await AuthenticationState!;
        var loggedUserLogin = authState.User.Claims.First(x => x.Type == ClaimTypes.Name).Value;
        LoggedUser = MemberService.GetOneByLogin(loggedUserLogin)!.ToViewModel();

        PageService.SetTitle("Docházka");

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
                attendance.Status = userAttendance.Status;
                attendance.Comment = userAttendance.Comment;
                attendance.GoogleEventId = userAttendance.GoogleEventId;
            }
        }

        Attendances = Attendances.OrderBy(x => x.Date).ToList();
    }

    private async Task LoadTableData()
    {
        // Načteme všechny členy – viditelnost v docházce řídí IsAttendanceVisible, ne IsVisible
        TableMembers = MemberService.GetAll()
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

        return userAttendance.Status switch
        {
            AttendanceStatus.Yes => "mud-theme-success",
            AttendanceStatus.Maybe => "mud-theme-warning",
            _ => "mud-theme-error"
        };
    }

    private int CountAttending(IEnumerable<MemberViewModel> members, DateTime date) =>
        members.SelectMany(x => x.Attendances)
            .Where(y => y.Date == date)
            .Count(y => y.Status == AttendanceStatus.Yes);

    // Počítá ženy označené jako tanečnice, které jsou přítomné.
    // Pokud je členka zároveň muzikantka, počítá se jako tanečnice jen pokud má ActivityRole = Dancer.
    private int CountAttendingFemaleDancers(DateTime date) =>
        FilteredFemales
            .Concat(FilteredExternals.Where(x => x.Gender == Gender.Female))
            .Where(m => m.IsDancer)
            .Count(m => m.Attendances.Any(a =>
                a.Date == date && a.Status == AttendanceStatus.Yes &&
                (!m.IsMusician || a.ActivityRole == AttendanceActivityRole.Dancer)));

    // Totéž pro muže tanečníky.
    private int CountAttendingMaleDancers(DateTime date) =>
        FilteredMales
            .Concat(FilteredExternals.Where(x => x.Gender == Gender.Male))
            .Where(m => m.IsDancer)
            .Count(m => m.Attendances.Any(a =>
                a.Date == date && a.Status == AttendanceStatus.Yes &&
                (!m.IsMusician || a.ActivityRole == AttendanceActivityRole.Dancer)));

    // Počítá všechny přítomné muzikanty (obě pohlaví).
    // Pokud je muzikant zároveň tanečník, počítá se jako muzikant jen pokud ActivityRole != Dancer (nebo null = default).
    private int CountAttendingMusicians(DateTime date) =>
        TableMembers
            .Where(x => x.IsMusician)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible)
            .Count(m => m.Attendances.Any(a =>
                a.Date == date && a.Status == AttendanceStatus.Yes &&
                (!m.IsDancer || a.ActivityRole != AttendanceActivityRole.Dancer)));

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

    private static readonly CultureInfo CsCulture = new("cs-CZ");

    private string MonthLabel => CurrentMonth.ToString("MMMM yyyy", CsCulture);

    private string FormatDayAbbr(DateTime date) => date.ToString("ddd", CsCulture);

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
            model.Member = member;
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

        // Zachytit existující Google Event ID před uložením (pro případné smazání)
        var previousGoogleEventId = model.Id != 0
            ? LoggedUser.Attendances.FirstOrDefault(a => a.Id == model.Id)?.GoogleEventId
            : null;

        bool isSelf = model.MemberId == LoggedUser.Id;
        AttendanceStatus statusAfterSave = AttendanceStatus.No;

        try
        {
            var attendanceResult = result.Data as AttendanceViewModel;
            if (attendanceResult is null)
            {
                if (model.Id != 0)
                    await AttendanceService.DeleteAsync(model.Id);
                Snackbar.Add("Docházka resetována.", Severity.Info);
                statusAfterSave = AttendanceStatus.No;
            }
            else
            {
                await AttendanceService.CreateOrUpdateAsync(attendanceResult.ToEntity());
                Snackbar.Add("Docházka uložena.", Severity.Success);
                statusAfterSave = attendanceResult.Status;
                model.Id = attendanceResult.Id;
            }
            if (refreshUserData) await LoadData();
            await LoadTableData();
        }
        catch
        {
            Snackbar.Add("Chyba při ukládání.", Severity.Error);
            return;
        }

        // Google Calendar sync – pouze pro vlastní docházku přihlášeného člena
        if (isSelf)
        {
            try
            {
                await SyncGoogleCalendarAsync(model, statusAfterSave, previousGoogleEventId);
            }
            catch
            {
                Snackbar.Add("Docházka uložena, ale synchronizace s Google Calendar selhala.", Severity.Warning);
            }
        }
    }

    private async Task SyncGoogleCalendarAsync(AttendanceViewModel model, AttendanceStatus status, string? previousGoogleEventId)
    {
        // Načteme čerstvá data člena pro aktuální Google tokeny
        var member = await MemberService.GetOneAsync(LoggedUser.Id);
        if (string.IsNullOrEmpty(member.GoogleRefreshToken) || string.IsNullOrEmpty(member.GoogleCalendarId))
            return;

        if (status == AttendanceStatus.Yes)
        {
            // Pokud událost v kalendáři již existuje, neopakuj vytvoření (ochrana proti duplikátům)
            if (!string.IsNullOrEmpty(model.GoogleEventId))
                return;

            var title = model.Event is not null
                ? model.Event.Name
                : $"Zkouška Demizon – {model.Date:d. M. yyyy}";

            var eventDate = model.Event is not null ? model.Event.DateFrom : model.Date;

            var createdId = await GoogleCalendarService.CreateEventAsync(
                member.GoogleRefreshToken,
                member.GoogleCalendarId,
                eventDate,
                title);

            if (createdId is not null && model.Id != 0)
            {
                var attendance = await AttendanceService.GetOneAsync(model.Id);
                attendance.GoogleEventId = createdId;
                await AttendanceService.CreateOrUpdateAsync(attendance);
            }
        }
        else if (!string.IsNullOrEmpty(previousGoogleEventId))
        {
            await GoogleCalendarService.DeleteEventAsync(
                member.GoogleRefreshToken,
                member.GoogleCalendarId,
                previousGoogleEventId);

            if (model.Id != 0)
            {
                try
                {
                    var attendance = await AttendanceService.GetOneAsync(model.Id);
                    attendance.GoogleEventId = null;
                    await AttendanceService.CreateOrUpdateAsync(attendance);
                }
                catch
                {
                    // Záznam docházky byl smazán (reset) – ignorujeme
                }
            }
        }
    }
}
