using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Contracts.Attendances;
using Demizon.Contracts.Events;
using Demizon.Contracts.Rehearsals;
using Demizon.Core.Services.Rehearsal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Services.Notification;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit;

/// <summary>
/// Všechna místa, která dřív počítala pátky sama, teď berou zkoušky
/// z <see cref="IRehearsalService"/>. Tady se hlídá, že je opravdu berou —
/// hlavně vícedenní akce, kterou dřív neviděla žádná z pěti kopií.
/// Červen 2026 má pátky 5., 12., 19. a 26.
/// </summary>
[Collection("WebHost")]
public class RehearsalApiTests : IAsyncLifetime
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

    private static DateTime T(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0);

    /// <summary>Pátek aspoň dva týdny dopředu, aby ruční připomínka nebyla „už proběhla“.</summary>
    private static DateTime FutureFriday()
    {
        var day = DateTime.Today.AddDays(14);
        while (day.DayOfWeek != DayOfWeek.Friday)
            day = day.AddDays(1);
        return day;
    }

    private async Task AddEventAsync(string name, DateTime from, DateTime to)
    {
        await using var db = _factory.NewContext();
        db.Events.Add(new Event { Name = name, DateFrom = from, DateTo = to });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------ měsíční tabulka

    [Fact]
    public async Task Tabulka_nema_sloupec_zkousky_v_patek_vicedenni_akce()
    {
        await AddEventAsync("Festival", T(6, 11, 17), T(6, 14, 16));

        var table = await _client.GetFromJsonAsync<MonthlyAttendanceTableDto>(
            "/api/attendances/table?year=2026&month=6");

        var rehearsalDays = table!.Columns.Where(c => c.EventId is null).Select(c => c.Date.Day);
        Assert.Equal([5, 19, 26], rehearsalDays);
    }

    [Fact]
    public async Task Sloupec_akce_je_IsEvent_a_sloupec_zkousky_ne()
    {
        var table = await _client.GetFromJsonAsync<MonthlyAttendanceTableDto>(
            "/api/attendances/table?year=2026&month=6");

        Assert.All(table!.Columns, c => Assert.Equal(c.EventId is not null, c.IsEvent));
        Assert.Contains(table.Columns, c => c.IsEvent);
        Assert.Contains(table.Columns, c => !c.IsEvent);
    }

    // ------------------------------------------------ seznam měsíce

    [Fact]
    public async Task Mesic_vraci_zkousku_se_skutecnym_casem()
    {
        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/events/month?year=2026&month=6");

        var rehearsal = events!.Single(e => e.IsRehearsal && e.DateFrom.Day == 5);
        Assert.Equal(T(6, 5, 19), rehearsal.DateFrom);
        Assert.Equal(T(6, 5, 21, 30), rehearsal.DateTo);
        Assert.False(rehearsal.IsCancelled);
    }

    [Fact]
    public async Task Mesic_nema_zkousku_v_patek_vicedenni_akce()
    {
        await AddEventAsync("Festival", T(6, 11, 17), T(6, 14, 16));

        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/events/month?year=2026&month=6");

        Assert.Equal([5, 19, 26], events!.Where(e => e.IsRehearsal).Select(e => e.DateFrom.Day));
    }

    // ------------------------------------------------ ruční připomínka

    [Fact]
    public async Task Rucni_pripominka_na_patek_s_akci_vrati_404()
    {
        // Dřív kontrolovala jen den v týdnu a poslala členům „zkouška“ na den vystoupení.
        var friday = FutureFriday();
        await AddEventAsync("Vystoupení", friday.AddHours(18), friday.AddHours(22));

        var response = await _client.PostAsync(
            $"/api/events/rehearsals/notify-missing-attendance?date={friday:yyyy-MM-dd}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = _factory.NewContext();
        Assert.Empty(await db.SentNotifications.ToListAsync());
    }

    [Fact]
    public async Task Rucni_pripominka_na_patek_bez_akce_projde()
    {
        var friday = FutureFriday();

        var response = await _client.PostAsync(
            $"/api/events/rehearsals/notify-missing-attendance?date={friday:yyyy-MM-dd}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = _factory.NewContext();
        var sent = Assert.Single(await db.SentNotifications.ToListAsync());
        Assert.Equal(friday, sent.RehearsalDate);
    }

    [Fact]
    public async Task Rucni_pripominka_na_jiny_den_nez_patek_vrati_404()
    {
        var thursday = FutureFriday().AddDays(-1);

        var response = await _client.PostAsync(
            $"/api/events/rehearsals/notify-missing-attendance?date={thursday:yyyy-MM-dd}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------ uložení docházky

    private Task<HttpResponseMessage> UpsertRehearsalAsync(DateTime day, string status = "yes") =>
        _client.PutAsJsonAsync(
            $"/api/attendances/rehearsal?date={day:yyyy-MM-dd}",
            new { status, comment = (string?)null, activityRole = (string?)null });

    private Task<HttpResponseMessage> UpsertMemberRehearsalAsync(DateTime day) =>
        _client.PutAsJsonAsync(
            $"/api/attendances/rehearsal/member/{_factory.MemberId}?date={day:yyyy-MM-dd}",
            new { status = "yes", comment = (string?)null, activityRole = (string?)null });

    [Fact]
    public async Task Dochazka_na_zkousku_v_patek_se_ulozi()
    {
        var response = await UpsertRehearsalAsync(T(6, 5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = _factory.NewContext();
        var stored = Assert.Single(await db.Attendances.Where(a => a.EventId == null).ToListAsync());
        Assert.Equal(T(6, 5), stored.Date);
    }

    [Fact]
    public async Task Dochazka_na_zkousku_v_utery_vrati_400_a_nic_nezalozi()
    {
        var response = await UpsertRehearsalAsync(T(6, 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.NewContext();
        Assert.Empty(await db.Attendances.Where(a => a.EventId == null).ToListAsync());
        // Validace běží před synchronizací, takže v Google nic nevzniklo.
        Assert.Empty(_factory.State.CreatedEvents);
    }

    [Fact]
    public async Task Dochazka_na_zkousku_v_patek_s_akci_vrati_400()
    {
        await AddEventAsync("Festival", T(6, 11, 17), T(6, 14, 16));

        var own = await UpsertRehearsalAsync(T(6, 12));
        var byAdmin = await UpsertMemberRehearsalAsync(T(6, 12));

        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, byAdmin.StatusCode);
        Assert.Empty(_factory.State.CreatedEvents);
    }

    // ------------------------------------------------ automatické připomínky

    private async Task RunRehearsalRemindersAsync(DateTime now)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await UnifiedNotificationService.CheckRehearsalRemindersAsync(
            services.GetRequiredService<Dal.DemizonContext>(),
            services.GetRequiredService<AttendanceReminderService>(),
            services.GetRequiredService<IRehearsalService>(),
            now,
            CancellationToken.None);
    }

    [Fact]
    public async Task Automaticka_pripominka_5_dni_pred_zkouskou_odejde()
    {
        await RunRehearsalRemindersAsync(T(6, 8, 10));

        await using var db = _factory.NewContext();
        var sent = Assert.Single(await db.SentNotifications.ToListAsync());
        Assert.Equal(T(6, 12), sent.RehearsalDate);
        Assert.Equal(NotificationType.RehearsalReminder5Days, sent.NotificationType);
    }

    [Fact]
    public async Task Automaticka_pripominka_preskoci_patek_vicedenni_akce()
    {
        await AddEventAsync("Festival", T(6, 11, 17), T(6, 14, 16));

        await RunRehearsalRemindersAsync(T(6, 8, 10));

        await using var db = _factory.NewContext();
        Assert.Empty(await db.SentNotifications.ToListAsync());
    }

    // ------------------------------------------------ výjimky

    private Task<HttpResponseMessage> CancelAsync(DateTime from, DateTime to, string? reason = null) =>
        _client.PostAsJsonAsync("/api/rehearsals/cancellations", new CancelRehearsalsRequest(
            DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), reason));

    [Fact]
    public async Task Zruseni_zkousky_vrati_201_a_vyjimka_je_v_seznamu()
    {
        var response = await CancelAsync(T(6, 12), T(6, 12), "Hody");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RehearsalCancellationDto>();
        Assert.Equal(new DateOnly(2026, 6, 12), created!.DateFrom);
        Assert.Equal("Hody", created.Reason);
        Assert.Equal("GCal Clen", created.CreatedBy);

        var list = await _client.GetFromJsonAsync<List<RehearsalCancellationDto>>(
            "/api/rehearsals/cancellations?from=2026-06-01&to=2026-06-30");
        Assert.Equal(created.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task Zrusena_zkouska_je_v_tabulce_i_v_mesici_oznacena_s_duvodem()
    {
        await CancelAsync(T(6, 12), T(6, 12), "Hody");

        var table = await _client.GetFromJsonAsync<MonthlyAttendanceTableDto>(
            "/api/attendances/table?year=2026&month=6");
        var column = table!.Columns.Single(c => c.EventId is null && c.Date.Day == 12);
        Assert.True(column.IsCancelled);
        Assert.Equal("Hody", column.CancellationReason);

        var events = await _client.GetFromJsonAsync<List<EventDto>>("/api/events/month?year=2026&month=6");
        var rehearsal = events!.Single(e => e.IsRehearsal && e.DateFrom.Day == 12);
        Assert.True(rehearsal.IsCancelled);
        Assert.Equal("Hody", rehearsal.CancellationReason);
    }

    [Fact]
    public async Task Dochazka_na_zrusenou_zkousku_vrati_400_se_zpravou()
    {
        await CancelAsync(T(6, 12), T(6, 12));

        var response = await UpsertRehearsalAsync(T(6, 12));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("zrušená", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rucni_pripominka_na_zrusenou_zkousku_vrati_400()
    {
        var friday = FutureFriday();
        await CancelAsync(friday, friday);

        var response = await _client.PostAsync(
            $"/api/events/rehearsals/notify-missing-attendance?date={friday:yyyy-MM-dd}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Automaticka_pripominka_preskoci_zrusenou_zkousku()
    {
        await CancelAsync(T(6, 12), T(6, 12));

        await RunRehearsalRemindersAsync(T(6, 8, 10));

        await using var db = _factory.NewContext();
        Assert.Empty(await db.SentNotifications.ToListAsync());
    }

    [Fact]
    public async Task Obnoveni_vyjimky_vrati_zkousku()
    {
        var created = await (await CancelAsync(T(6, 12), T(6, 12)))
            .Content.ReadFromJsonAsync<RehearsalCancellationDto>();

        var response = await _client.DeleteAsync($"/api/rehearsals/cancellations/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var table = await _client.GetFromJsonAsync<MonthlyAttendanceTableDto>(
            "/api/attendances/table?year=2026&month=6");
        Assert.DoesNotContain(table!.Columns, c => c.IsCancelled);
    }

    [Fact]
    public async Task Zruseni_obdobi_bez_zkousky_vrati_400()
    {
        var response = await CancelAsync(T(6, 9), T(6, 11));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Obnoveni_neexistujici_vyjimky_vrati_404()
    {
        var response = await _client.DeleteAsync("/api/rehearsals/cancellations/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Zrusit_zkousku_smi_jen_admin()
    {
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Dal.DemizonContext>();
            var member = new Member
            {
                Name = "Bez", Surname = "Prav", Login = "bez-prav", Email = "bez-prav@demizon.test",
                PasswordHash = "x", Role = UserRole.Standard, Gender = Gender.Female,
            };
            db.Members.Add(member);
            await db.SaveChangesAsync();
            token = scope.ServiceProvider
                .GetRequiredService<Core.Services.Authentication.TokenService>()
                .GenerateToken(member);
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/rehearsals/cancellations",
            new CancelRehearsalsRequest(new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var list = await client.GetFromJsonAsync<List<RehearsalCancellationDto>>(
            "/api/rehearsals/cancellations?from=2026-06-01&to=2026-06-30");
        Assert.Empty(list!);
    }
}
