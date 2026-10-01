using Demizon.Common.Configuration;
using Demizon.Core.Services.Rehearsal;
using Demizon.Dal;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Options;

namespace Demizon.Tests.Integration;

/// <summary>
/// Načtení akcí pro <see cref="RehearsalSchedule"/>. Samotné pravidlo hlídají
/// unit testy, tady jde o dotaz: které akce do rozsahu zasahují.
/// Červen 2026 má pátky 5., 12., 19. a 26.
/// </summary>
public class RehearsalServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "tester");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private static RehearsalService Service(DemizonContext db) =>
        new(db,
            Options.Create(new RehearsalSettings()),
            Options.Create(new GoogleCalendarSettings { RedirectUri = "https://demizon.test/google/callback" }));

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static DateTime T(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0);

    private async Task SeedEventsAsync(params Dal.Entities.Event[] events)
    {
        await using var db = _fixture.NewContext();
        db.Events.AddRange(events);
        await db.SaveChangesAsync();
    }

    private async Task<int[]> JuneDaysAsync()
    {
        await using var db = _fixture.NewContext();
        var occurrences = await Service(db).GetRangeAsync(D(6, 1), D(7, 1));
        return occurrences.Select(o => o.Date.Day).ToArray();
    }

    [Fact]
    public async Task Bez_akci_jsou_zkousky_vsechny_patky()
    {
        var days = await JuneDaysAsync();

        Assert.Equal([5, 12, 19, 26], days);
    }

    [Fact]
    public async Task Vicedenni_akce_schova_patek_uprostred()
    {
        await SeedEventsAsync(TestData.Event("Festival", T(6, 11, 17), T(6, 14, 16)));

        var days = await JuneDaysAsync();

        Assert.Equal([5, 19, 26], days);
    }

    [Fact]
    public async Task Akce_zacinajici_v_minulem_mesici_schova_prvni_patek()
    {
        // Dotaz jen na DateFrom v měsíci by tuhle akci vůbec nenačetl.
        await SeedEventsAsync(TestData.Event("Soustředění", T(5, 29, 18), T(6, 6, 12)));

        var days = await JuneDaysAsync();

        Assert.Equal([12, 19, 26], days);
    }

    [Fact]
    public async Task Akce_konci_pred_rozsahem_nic_neschova()
    {
        await SeedEventsAsync(TestData.Event("Květen", T(5, 1, 18), T(5, 31, 22)));

        var days = await JuneDaysAsync();

        Assert.Equal([5, 12, 19, 26], days);
    }

    [Fact]
    public async Task Zrusena_akce_zkousku_neschova()
    {
        var cancelled = TestData.Event("Zrušené vystoupení", T(6, 12, 18), T(6, 12, 22));
        cancelled.IsCancelled = true;
        await SeedEventsAsync(cancelled);

        var days = await JuneDaysAsync();

        Assert.Equal([5, 12, 19, 26], days);
    }

    [Fact]
    public async Task GetAsync_vrati_null_pro_jiny_den_a_pro_patek_s_akci()
    {
        await SeedEventsAsync(TestData.Event("Vystoupení", T(6, 12, 18), T(6, 12, 22)));
        await using var db = _fixture.NewContext();
        var service = Service(db);

        Assert.Null(await service.GetAsync(D(6, 11)));
        Assert.Null(await service.GetAsync(D(6, 12)));

        var rehearsal = await service.GetAsync(D(6, 19));
        Assert.NotNull(rehearsal);
        Assert.Equal(T(6, 19, 19), rehearsal!.StartsAt);
    }
}
