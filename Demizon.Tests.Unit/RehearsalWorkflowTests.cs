using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Contracts.Events;
using Demizon.Contracts.Rehearsals;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.Unit;

/// <summary>
/// Důsledky změn kalendáře zkoušek mimo databázi (<c>RehearsalWorkflow</c>):
/// události v Google Calendaru členů a volitelný push. Člen z
/// <see cref="CalendarApiFactory"/> má propojený (falešný) kalendář.
/// Červen 2026 má pátky 5., 12., 19. a 26.
/// </summary>
[Collection("WebHost")]
public class RehearsalWorkflowTests : IAsyncLifetime
{
    private static readonly DateTime Friday = new(2026, 6, 12);

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

    private async Task SayYesToRehearsalAsync()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/attendances/rehearsal?date={Friday:yyyy-MM-dd}",
            new { status = "yes", comment = (string?)null, activityRole = (string?)null });
        response.EnsureSuccessStatusCode();
        Assert.Equal(["gcal-event-1"], _factory.State.CreatedEvents);
    }

    private async Task<Attendance> StoredRehearsalAttendanceAsync()
    {
        await using var db = _factory.NewContext();
        return await db.Attendances.AsNoTracking().SingleAsync(a => a.EventId == null && a.Date == Friday);
    }

    private async Task<RehearsalCancellationDto> CancelFridayAsync(bool notify = false)
    {
        var response = await _client.PostAsJsonAsync("/api/rehearsals/cancellations",
            new CancelRehearsalsRequest(DateOnly.FromDateTime(Friday), DateOnly.FromDateTime(Friday), "Hody", notify));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RehearsalCancellationDto>())!;
    }

    private async Task<List<SentNotification>> SentAsync()
    {
        await using var db = _factory.NewContext();
        return await db.SentNotifications.AsNoTracking().ToListAsync();
    }

    // ------------------------------------------------ zrušení a obnovení zkoušky

    [Fact]
    public async Task Zruseni_zkousky_smaze_udalost_v_Google_a_dochazku_necha()
    {
        await SayYesToRehearsalAsync();

        await CancelFridayAsync();

        Assert.Equal(["gcal-event-1"], _factory.State.DeletedEvents);
        var stored = await StoredRehearsalAttendanceAsync();
        Assert.Null(stored.GoogleEventId);
        Assert.Equal(AttendanceStatus.Yes, stored.Status);
    }

    [Fact]
    public async Task Obnoveni_zkousky_vrati_udalost_do_Google()
    {
        await SayYesToRehearsalAsync();
        var cancellation = await CancelFridayAsync();
        _factory.State.CreatedEventId = "gcal-event-2";

        var response = await _client.DeleteAsync($"/api/rehearsals/cancellations/{cancellation.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["gcal-event-1", "gcal-event-2"], _factory.State.CreatedEvents);
        Assert.Equal("gcal-event-2", (await StoredRehearsalAttendanceAsync()).GoogleEventId);
    }

    [Fact]
    public async Task Zruseni_bez_notify_push_neposle()
    {
        await CancelFridayAsync(notify: false);

        Assert.Empty(await SentAsync());
    }

    [Fact]
    public async Task Zruseni_s_notify_upozorni_toho_kdo_nema_ne()
    {
        await CancelFridayAsync(notify: true);

        var sent = Assert.Single(await SentAsync());
        Assert.Equal(_factory.MemberId, sent.MemberId);
        Assert.Equal(NotificationType.RehearsalCancelled, sent.NotificationType);
        Assert.Equal(Friday, sent.RehearsalDate);
    }

    [Fact]
    public async Task Zruseni_s_notify_vynecha_toho_kdo_ma_ne()
    {
        await _client.PutAsJsonAsync(
            $"/api/attendances/rehearsal?date={Friday:yyyy-MM-dd}",
            new { status = "no", comment = (string?)null, activityRole = (string?)null });

        await CancelFridayAsync(notify: true);

        Assert.Empty(await SentAsync());
    }

    // ------------------------------------------------ akce na pátek

    private async Task<int> CreateFridayEventAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest(
            "Vystoupení", Friday.AddHours(18), Friday.AddHours(22), null, "none"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventDto>())!.Id;
    }

    [Fact]
    public async Task Akce_zalozena_na_patek_se_zkouskou_ano_smaze_udalost_zkousky()
    {
        // Dřív v Google zůstala viset „Zkouška Demizon“ na den vystoupení.
        await SayYesToRehearsalAsync();

        await CreateFridayEventAsync();

        Assert.Equal(["gcal-event-1"], _factory.State.DeletedEvents);
        Assert.Null((await StoredRehearsalAttendanceAsync()).GoogleEventId);
    }

    [Fact]
    public async Task Smazani_akce_vrati_udalost_zkousky()
    {
        await SayYesToRehearsalAsync();
        var eventId = await CreateFridayEventAsync();
        _factory.State.CreatedEventId = "gcal-event-2";

        var response = await _client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("gcal-event-2", (await StoredRehearsalAttendanceAsync()).GoogleEventId);
    }

    [Fact]
    public async Task Zruseni_akce_vrati_zkousku_i_do_Google()
    {
        await SayYesToRehearsalAsync();
        var eventId = await CreateFridayEventAsync();
        _factory.State.CreatedEventId = "gcal-event-2";

        var response = await _client.PatchAsync($"/api/events/{eventId}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("gcal-event-2", (await StoredRehearsalAttendanceAsync()).GoogleEventId);
        Assert.Empty(await SentAsync());
    }

    [Fact]
    public async Task Zruseni_akce_s_notify_posle_EventCancelled()
    {
        var eventId = await CreateFridayEventAsync();

        var response = await _client.PatchAsync($"/api/events/{eventId}/cancel?notify=true", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(await SentAsync());
        Assert.Equal(NotificationType.EventCancelled, sent.NotificationType);
        Assert.Equal(eventId, sent.EventId);
    }

    [Fact]
    public async Task Obnoveni_akce_s_notify_push_neposle()
    {
        var eventId = await CreateFridayEventAsync();
        await _client.PatchAsync($"/api/events/{eventId}/cancel", null);

        await _client.PatchAsync($"/api/events/{eventId}/cancel?notify=true", null);

        Assert.Empty(await SentAsync());
    }
}
