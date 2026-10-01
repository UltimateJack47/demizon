using System.Globalization;
using Demizon.Common;
using System.Security.Claims;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Core.Services.Rehearsal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Pages.Admin.Attendance.Components;
using Demizon.Mvc.Services.Rehearsal;
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
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private RehearsalWorkflow RehearsalWorkflow { get; set; } = null!;

    // Aktuálně zobrazený měsíc
    private DateTime CurrentMonth { get; set; }
    private DateTime StartDate => new(CurrentMonth.Year, CurrentMonth.Month, 1);

    // Poslední okamžik měsíce. Dřív to byla půlnoc posledního dne, takže akce
    // 31. v 18:00 (a docházka na ni, Date = DateFrom) z tabulky vypadla.
    private DateTime EndDate => StartDate.AddMonths(1).AddTicks(-1);

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
        var events = EventService.GetAll()
            .Where(x => x.DateFrom >= StartDate && x.DateFrom <= EndDate)
            .ToList()
            .Select(x => x.ToViewModel())
            .ToList();

        var monthStart = DateOnly.FromDateTime(StartDate);
        var rehearsals = await RehearsalService.GetRangeAsync(monthStart, monthStart.AddMonths(1));

        Attendances = rehearsals
            .Select(r => new AttendanceViewModel
            {
                Member = LoggedUser,
                MemberId = LoggedUser.Id,
                Date = r.Day,
                Rehearsal = r,
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
            var userAttendance = LoggedUser.Attendances.FirstOrDefault(x => Matches(x, attendance));
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

    /// <summary>
    /// Patří docházka do sloupce? Akce podle <c>EventId</c>, zkouška podle dne a bez
    /// <c>EventId</c>. Dřív se párovalo jen podle data, a to se rozbije, jakmile má
    /// zrušená akce zadaná jen datem (pátek 00:00) vedle sebe vrácenou zkoušku.
    /// </summary>
    private static bool Matches(AttendanceViewModel attendance, AttendanceViewModel col) =>
        col.EventId.HasValue
            ? attendance.EventId == col.EventId
            : attendance.EventId is null && attendance.Date == col.Date;

    /// <summary>Docházka člena ve sloupci. Na zrušené zkoušce se neukazuje (ale v DB zůstává).</summary>
    private static AttendanceViewModel? CellOf(MemberViewModel member, AttendanceViewModel col) =>
        col.Rehearsal?.IsCancelled == true
            ? null
            : member.Attendances.FirstOrDefault(x => Matches(x, col));

    private static bool AttendsAs(AttendanceViewModel a, AttendanceViewModel col) =>
        a.Status == AttendanceStatus.Yes && Matches(a, col) && col.Rehearsal?.IsCancelled != true;

    private int CountAttending(IEnumerable<MemberViewModel> members, AttendanceViewModel col) =>
        members.SelectMany(x => x.Attendances)
            .Count(y => AttendsAs(y, col));

    // Počítá ženy označené jako tanečnice, které jsou přítomné.
    // Pokud je členka zároveň muzikantka, počítá se jako tanečnice jen pokud má ActivityRole = Dancer.
    private int CountAttendingFemaleDancers(AttendanceViewModel col) =>
        FilteredFemales
            .Concat(FilteredExternals.Where(x => x.Gender == Gender.Female))
            .Where(m => m.IsDancer)
            .Count(m => m.Attendances.Any(a =>
                AttendsAs(a, col) &&
                (!m.IsMusician || a.ActivityRole == AttendanceActivityRole.Dancer)));

    // Totéž pro muže tanečníky.
    private int CountAttendingMaleDancers(AttendanceViewModel col) =>
        FilteredMales
            .Concat(FilteredExternals.Where(x => x.Gender == Gender.Male))
            .Where(m => m.IsDancer)
            .Count(m => m.Attendances.Any(a =>
                AttendsAs(a, col) &&
                (!m.IsMusician || a.ActivityRole == AttendanceActivityRole.Dancer)));

    // Počítá všechny přítomné muzikanty (obě pohlaví).
    // Pokud je muzikant zároveň tanečník, počítá se jako muzikant jen pokud ActivityRole != Dancer (nebo null = default).
    private int CountAttendingMusicians(AttendanceViewModel col) =>
        TableMembers
            .Where(x => x.IsMusician)
            .Where(x => GenderFilter == null || x.Gender == GenderFilter)
            .Where(x => ShowAttendanceHidden || x.IsAttendanceVisible)
            .Count(m => m.Attendances.Any(a =>
                AttendsAs(a, col) &&
                (!m.IsDancer || a.ActivityRole != AttendanceActivityRole.Dancer)));

    // Rychlá akce z hlavičky sloupce zkoušky (jen admin)
    private async Task CancelRehearsalAsync(AttendanceViewModel col)
    {
        if (await RehearsalAdminActions.CancelAsync(DialogService, Snackbar, RehearsalWorkflow, LoggedUser.Id, col.Date))
        {
            await LoadData();
            await LoadTableData();
        }
    }

    private async Task RestoreRehearsalAsync(AttendanceViewModel col)
    {
        if (col.Rehearsal?.CancellationId is not { } cancellationId)
            return;

        var cancellation = await RehearsalService.GetCancellationAsync(cancellationId);
        if (cancellation is null)
        {
            // Mezitím ji obnovil někdo jiný.
            await LoadData();
            return;
        }

        if (await RehearsalAdminActions.RestoreAsync(DialogService, Snackbar, RehearsalWorkflow, cancellation))
        {
            await LoadData();
            await LoadTableData();
        }
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

    private static readonly CultureInfo CsCulture = new("cs-CZ");

    private string MonthLabel => CurrentMonth.ToString("MMMM yyyy", CsCulture);

    private string FormatDayAbbr(DateTime date) => date.ToString("ddd", CsCulture);

    // Kliknutí na buňku konkrétního člena v tabulce (admin může editovat kohokoliv)
    private async Task SetAttendanceMember(MemberViewModel member, AttendanceViewModel col)
    {
        var existing = member.Attendances.FirstOrDefault(x => Matches(x, col));

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

        // AttendanceForm binduje přímo na předaný objekt (@bind-Value="Model.Status")
        // a vrací tutéž instanci, takže model je po zavření dialogu už přepsaný —
        // a u existující docházky je to zároveň řádek v mřížce. Když se uložení
        // nepovede, musí se zobrazení přebudovat z databáze, jinak by buňka ukazovala
        // hodnotu, která nikde není uložená.
        async Task RefreshViewAsync()
        {
            if (refreshUserData) await LoadData();
            await LoadTableData();
        }

        try
        {
            var attendanceResult = result.Data as AttendanceViewModel;
            if (attendanceResult is null)
            {
                // Služby výjimku spolykají a vrátí neúspěch, takže catch níž se
                // neuplatní — návratovou hodnotu je nutné kontrolovat.
                if (model.Id != 0)
                {
                    var reset = await AttendanceService.DeleteAsync(model.Id);
                    if (!reset.IsSuccess)
                    {
                        Snackbar.Add(reset.Error ?? "Docházku se nepodařilo resetovat.", Severity.Error);
                        await RefreshViewAsync();
                        return;
                    }
                }

                Snackbar.Add("Docházka resetována.", Severity.Info);
                statusAfterSave = AttendanceStatus.No;
            }
            else
            {
                // Bez téhle kontroly by se pokračovalo k synchronizaci s Google
                // Calendarem a vznikla by tam reálná událost pro docházku, která se
                // neuložila — a nešla by už smazat, protože ID události se ukládá
                // k docházkovému řádku, který neexistuje.
                //
                // Klíč bere volající z Result.Value. Dřív se sem psalo
                // model.Id = attendanceResult.Id, což byl no-op: attendanceResult JE
                // model a ToEntity() vyrábí jinou entitu, na kterou klíč přiřadila
                // databáze. U nové docházky tak model.Id zůstalo nulové a ID
                // vytvořené události se nemělo kam zapsat.
                var saved = await AttendanceService.CreateOrUpdateAsync(attendanceResult.ToEntity());
                if (!saved.IsSuccess)
                {
                    Snackbar.Add(saved.Error ?? "Docházku se nepodařilo uložit.", Severity.Error);
                    await RefreshViewAsync();
                    return;
                }

                Snackbar.Add("Docházka uložena.", Severity.Success);
                statusAfterSave = attendanceResult.Status;
                model.Id = saved.Value;
            }
            await RefreshViewAsync();
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
            var eventDateTo = model.Event?.DateTo;

            var createdId = await GoogleCalendarService.CreateEventAsync(
                member.GoogleRefreshToken,
                member.GoogleCalendarId,
                eventDate,
                eventDateTo,
                title);

            if (createdId is null)
                return;

            var linked = false;
            if (model.Id != 0)
            {
                try
                {
                    var attendance = await AttendanceService.GetOneAsync(model.Id);
                    attendance.GoogleEventId = createdId;
                    // Návratovou hodnotu je nutné kontrolovat: služba výjimku spolkne
                    // a vrátí neúspěch, takže bez kontroly by událost zůstala
                    // v kalendáři bez odkazu z docházky.
                    linked = (await AttendanceService.CreateOrUpdateAsync(attendance)).IsSuccess;
                }
                catch
                {
                    // GetOneAsync hodí, když docházku mezitím někdo smazal. Výjimku
                    // nesmíme pustit dál — úklid nesparovane události níž musí proběhnout,
                    // jinak by v kalendáři zůstala natrvalo.
                    linked = false;
                }
            }

            if (linked)
            {
                // Do view modelu taky, jinak by druhé uložení ve stejném zobrazení
                // prošlo ochranou proti duplikátům a vytvořilo událost podruhé.
                model.GoogleEventId = createdId;
                return;
            }

            // ID události se nemá kam uložit, takže bychom ji už nikdy nedohledali.
            // Radši ji hned zrušit než nechat v kalendáři natrvalo.
            await GoogleCalendarService.DeleteEventAsync(
                member.GoogleRefreshToken,
                member.GoogleCalendarId,
                createdId);
            Snackbar.Add(
                "Událost v Google Calendaru se nepodařilo propojit s docházkou, byla proto zrušena.",
                Severity.Warning);
        }
        else if (!string.IsNullOrEmpty(previousGoogleEventId))
        {
            await GoogleCalendarService.DeleteEventAsync(
                member.GoogleRefreshToken,
                member.GoogleCalendarId,
                previousGoogleEventId);

            if (model.Id != 0)
            {
                // Cílený UPDATE mimo change tracker. Událost už v kalendáři není,
                // takže docházka nesmí zůstat s jejím ID: nová událost se zakládá
                // jen při prázdném GoogleEventId, takže by příští „přijdu“ mlčky
                // žádnou nevytvořilo — a to natrvalo.
                var cleared = await AttendanceService.ClearGoogleEventIdAsync(model.Id);
                if (cleared.IsSuccess)
                {
                    model.GoogleEventId = null;
                }
                else if (cleared.ErrorKind != ResultErrorKind.NotFound)
                {
                    // NotFound = docházka byla mezitím resetovaná, což je v pořádku.
                    Snackbar.Add(
                        "Událost byla z kalendáře smazána, ale docházku se nepodařilo aktualizovat.",
                        Severity.Warning);
                }
            }
        }
    }
}
