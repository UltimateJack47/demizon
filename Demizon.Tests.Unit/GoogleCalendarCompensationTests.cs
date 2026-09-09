using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.Unit;

/// <summary>
/// Kompenzační logika mezi Google Calendarem a databází. Synchronizace běží
/// <b>před</b> uložením docházky, takže když uložení selže, obojí se rozejde —
/// a to v obou směrech. Controller to musí dorovnat, jinak vznikne trvalá
/// porucha, kterou uživatel nemá jak opravit.
/// </summary>
/// <remarks>
/// Testuje se přes dvojníka (<c>FakeGoogleCalendarService</c>) a obal nad
/// docházkovou službou, který na příkaz nechá zápis selhat. Bez toho se ten
/// stav nedá vyvolat — a právě proto tahle logika dosud běžela neotestovaná.
/// </remarks>
[Collection("WebHost")]
public class GoogleCalendarCompensationTests : IAsyncLifetime
{
    private CalendarApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new CalendarApiFactory();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.MemberToken());
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private Task<HttpResponseMessage> UpsertAsync(string status) =>
        _client.PutAsJsonAsync(
            $"/api/attendances/{_factory.EventId}",
            new { status, comment = (string?)null, activityRole = (string?)null });

    private async Task<Attendance?> StoredAttendanceAsync()
    {
        await using var db = _factory.NewContext();
        return await db.Attendances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.MemberId == _factory.MemberId && a.EventId == _factory.EventId);
    }

    // ------------------------------------------------ vytvoření události

    [Fact]
    public async Task Prijdu_vytvori_udalost_a_ulozi_jeji_ID()
    {
        var response = await UpsertAsync("yes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["gcal-event-1"], _factory.State.CreatedEvents);
        Assert.Empty(_factory.State.DeletedEvents);

        var stored = await StoredAttendanceAsync();
        Assert.NotNull(stored);
        Assert.Equal("gcal-event-1", stored!.GoogleEventId);
    }

    /// <summary>
    /// Tohle je ten scénář, na který kompenzace vznikla: událost v kalendáři
    /// existuje, ale ID se nemá kam zapsat. Nedohledatelná událost = událost,
    /// kterou už uživatel nikdy nesmaže, takže se ruší hned.
    /// </summary>
    [Fact]
    public async Task Kdyz_ulozeni_selze_vytvorena_udalost_se_zrusi()
    {
        _factory.State.FailAttendanceWrites = true;

        var response = await UpsertAsync("yes");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["gcal-event-1"], _factory.State.CreatedEvents);
        // Vytvořená událost musí být zrušená, ne ponechaná v kalendáři.
        Assert.Equal(["gcal-event-1"], _factory.State.DeletedEvents);

        Assert.Null(await StoredAttendanceAsync());
    }

    [Fact]
    public async Task Kdyz_kalendar_ID_nevrati_nic_se_neruzi()
    {
        _factory.State.CreatedEventId = null;
        _factory.State.FailAttendanceWrites = true;

        var response = await UpsertAsync("yes");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // Žádná událost nevznikla, takže není co rušit — a hlavně se nesmí
        // zkoušet mazat null.
        Assert.Empty(_factory.State.CreatedEvents);
        Assert.Empty(_factory.State.DeletedEvents);
    }

    // -------------------------------------------------- smazání události

    [Fact]
    public async Task Nepridu_smaze_udalost_a_vynuluje_ID()
    {
        Assert.Equal(HttpStatusCode.OK, (await UpsertAsync("yes")).StatusCode);

        var response = await UpsertAsync("no");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["gcal-event-1"], _factory.State.DeletedEvents);

        var stored = await StoredAttendanceAsync();
        Assert.NotNull(stored);
        Assert.Null(stored!.GoogleEventId);
        Assert.Equal(AttendanceStatus.No, stored.Status);
    }

    /// <summary>
    /// Druhý směr: událost je z kalendáře smazaná, ale uložení selže, takže
    /// <c>DiscardPendingChanges</c> vrátí <c>GoogleEventId</c> do řádku. Nová
    /// událost se zakládá <b>jen</b> při prázdném ID, takže bez dorovnání by
    /// příští „přijdu“ mlčky žádnou nevytvořilo — natrvalo.
    /// </summary>
    [Fact]
    public async Task Kdyz_ulozeni_selze_po_smazani_udalosti_ID_se_z_databaze_vynuluje()
    {
        Assert.Equal(HttpStatusCode.OK, (await UpsertAsync("yes")).StatusCode);
        Assert.Equal("gcal-event-1", (await StoredAttendanceAsync())!.GoogleEventId);

        _factory.State.FailAttendanceWrites = true;
        var response = await UpsertAsync("no");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["gcal-event-1"], _factory.State.DeletedEvents);
        Assert.Equal(1, _factory.State.ClearGoogleEventIdCalls);

        // A hlavně: v databázi po dorovnání ID neexistující události není.
        var stored = await StoredAttendanceAsync();
        Assert.NotNull(stored);
        Assert.Null(stored!.GoogleEventId);
    }

    [Fact]
    public async Task Pristi_prijdu_po_dorovnani_udalost_znovu_vytvori()
    {
        Assert.Equal(HttpStatusCode.OK, (await UpsertAsync("yes")).StatusCode);

        _factory.State.FailAttendanceWrites = true;
        await UpsertAsync("no");

        // Bez dorovnání by tady zůstalo staré ID a ochrana proti duplikátům
        // by novou událost umlčela.
        _factory.State.FailAttendanceWrites = false;
        _factory.State.CreatedEventId = "gcal-event-2";
        Assert.Equal(HttpStatusCode.OK, (await UpsertAsync("yes")).StatusCode);

        Assert.Contains("gcal-event-2", _factory.State.CreatedEvents);
        Assert.Equal("gcal-event-2", (await StoredAttendanceAsync())!.GoogleEventId);
    }

    [Fact]
    public async Task Selhani_dorovnani_request_neshodi()
    {
        Assert.Equal(HttpStatusCode.OK, (await UpsertAsync("yes")).StatusCode);

        _factory.State.FailAttendanceWrites = true;
        _factory.State.FailClearGoogleEventId = true;

        var response = await UpsertAsync("no");

        // Neúspěch dorovnání se loguje, ale odpověď zůstává tou o selhaném
        // uložení — ne 500 z neošetřené výjimky.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, _factory.State.ClearGoogleEventIdCalls);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", body);
    }

    // ------------------------------------------------------ odvolaný token

    /// <summary>
    /// Když Google token odvolá, uložené credentials se mají zapomenout
    /// a uživatel se to má dozvědět — jinak by se sync tiše pokoušel dál.
    /// </summary>
    [Fact]
    public async Task Odvolany_token_odpoji_kalendar_a_ulozeni_projde()
    {
        _factory.State.ThrowTokenRevoked = true;

        var response = await UpsertAsync("yes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-GCal-Warning"));

        await using var db = _factory.NewContext();
        var member = await db.Members.AsNoTracking().SingleAsync(m => m.Id == _factory.MemberId);
        Assert.Null(member.GoogleRefreshToken);
        Assert.Null(member.GoogleCalendarId);

        // Docházka se uložila, jen bez události v kalendáři.
        var stored = await StoredAttendanceAsync();
        Assert.NotNull(stored);
        Assert.Null(stored!.GoogleEventId);
    }
}
