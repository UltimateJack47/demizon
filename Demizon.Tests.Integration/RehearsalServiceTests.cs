using Demizon.Common;
using Demizon.Common.Configuration;
using Demizon.Core.Services.Rehearsal;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
            Options.Create(new GoogleCalendarSettings { RedirectUri = "https://demizon.test/google/callback" }),
            NullLogger<RehearsalService>.Instance);

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

    // ---------------------------------------------------------------- výjimky

    [Fact]
    public async Task Zrusena_zkouska_zustane_v_rozsahu_se_stavem_Cancelled_a_duvodem()
    {
        await using (var db = _fixture.NewContext())
        {
            var member = await TestData.SeedMemberAsync(db);
            var created = await Service(db).CancelAsync(D(6, 12), D(6, 12), "  Hody  ", member.Id);
            ResultAssert.Ok(created);
        }

        await using var verify = _fixture.NewContext();
        var occurrences = await Service(verify).GetRangeAsync(D(6, 1), D(7, 1));

        Assert.Equal(4, occurrences.Count);
        var cancelled = Assert.Single(occurrences, o => o.IsCancelled);
        Assert.Equal(D(6, 12), cancelled.Date);
        Assert.Equal("Hody", cancelled.CancellationReason);
    }

    [Fact]
    public async Task Vyjimka_pres_hranici_mesice_se_nacte_v_obou_mesicich()
    {
        await using (var db = _fixture.NewContext())
            ResultAssert.Ok(await Service(db).CancelAsync(D(6, 22), D(7, 10), "Prázdniny", null));

        await using var verify = _fixture.NewContext();
        var june = await Service(verify).GetRangeAsync(D(6, 1), D(7, 1));
        var july = await Service(verify).GetRangeAsync(D(7, 1), D(8, 1));

        var cancelledJune = june.Where(o => o.IsCancelled).Select(o => o.Date.Day).ToArray();
        var cancelledJuly = july.Where(o => o.IsCancelled).Select(o => o.Date.Day).ToArray();
        Assert.Equal([26], cancelledJune);
        Assert.Equal([3, 10], cancelledJuly);
    }

    [Fact]
    public async Task CancelAsync_odmitne_obdobi_bez_zkousky()
    {
        await SeedEventsAsync(TestData.Event("Vystoupení", T(6, 12, 18), T(6, 12, 22)));
        await using var db = _fixture.NewContext();
        var service = Service(db);

        ResultAssert.Failed(await service.CancelAsync(D(6, 9), D(6, 11), null, null), ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.CancelAsync(D(6, 12), D(6, 12), null, null), ResultErrorKind.Rejected);
        Assert.Empty(await db.RehearsalCancellations.ToListAsync());
    }

    [Fact]
    public async Task CancelAsync_odmitne_obraceny_rozsah_dlouhy_rozsah_a_dlouhy_duvod()
    {
        await using var db = _fixture.NewContext();
        var service = Service(db);

        ResultAssert.Failed(await service.CancelAsync(D(6, 19), D(6, 12), null, null), ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.CancelAsync(D(6, 1), D(6, 1).AddDays(RehearsalService.MaxCancellationDays), null, null), ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.CancelAsync(D(6, 12), D(6, 12), new string('x', 201), null), ResultErrorKind.Rejected);
        Assert.Empty(await db.RehearsalCancellations.ToListAsync());
    }

    [Fact]
    public async Task CancelAsync_odmitne_obdobi_kde_uz_je_vse_zruseno()
    {
        await using var db = _fixture.NewContext();
        var service = Service(db);
        ResultAssert.Ok(await service.CancelAsync(D(6, 1), D(6, 30), "Prázdniny", null));

        ResultAssert.Failed(await service.CancelAsync(D(6, 12), D(6, 12), "Hody", null), ResultErrorKind.Rejected);
    }

    [Fact]
    public async Task RestoreAsync_vrati_zkousku_a_docházka_na_ni_zustala()
    {
        int cancellationId;
        int memberId;
        await using (var db = _fixture.NewContext())
        {
            var member = await TestData.SeedMemberAsync(db);
            memberId = member.Id;
            db.Attendances.Add(TestData.RehearsalAttendance(member.Id, T(6, 12), AttendanceStatus.Yes));
            await db.SaveChangesAsync();
            cancellationId = (await Service(db).CancelAsync(D(6, 12), D(6, 12), null, member.Id)).Value;
        }

        await using (var db = _fixture.NewContext())
            ResultAssert.Ok(await Service(db).RestoreAsync(cancellationId));

        await using var verify = _fixture.NewContext();
        var rehearsal = await Service(verify).GetAsync(D(6, 12));
        Assert.NotNull(rehearsal);
        Assert.False(rehearsal!.IsCancelled);
        var attendance = Assert.Single(await verify.Attendances.ToListAsync());
        Assert.Equal(memberId, attendance.MemberId);
        Assert.Equal(AttendanceStatus.Yes, attendance.Status);
    }

    [Fact]
    public async Task RestoreAsync_neexistujici_vyjimky_vrati_NotFound()
    {
        await using var db = _fixture.NewContext();

        ResultAssert.Failed(await Service(db).RestoreAsync(999), ResultErrorKind.NotFound);
    }

    [Fact]
    public async Task Smazani_clena_vyjimku_nesmaze()
    {
        int memberId;
        await using (var db = _fixture.NewContext())
        {
            var member = await TestData.SeedMemberAsync(db);
            memberId = member.Id;
            ResultAssert.Ok(await Service(db).CancelAsync(D(6, 12), D(6, 12), null, member.Id));
        }

        await using (var db = _fixture.NewContext())
        {
            await db.Members.IgnoreQueryFilters().Where(m => m.Id == memberId).ExecuteDeleteAsync();
        }

        await using var verify = _fixture.NewContext();
        var cancellation = Assert.Single(await verify.RehearsalCancellations.ToListAsync());
        Assert.Null(cancellation.CreatedByMemberId);
    }
}
