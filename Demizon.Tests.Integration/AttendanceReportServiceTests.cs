using Demizon.Core.Services.Attendance;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;

namespace Demizon.Tests.Integration;

/// <summary>
/// Statistiky docházky stojí na doménovém pravidle z AGENTS.md: <b>zkoušky</b> jsou
/// docházkové řádky s <c>EventId == null</c>, <b>akce</b> mají <c>EventId</c> vyplněné.
/// Celkový počet zkoušek se počítá z <em>distinct dat</em>, celkový počet akcí
/// z <em>distinct EventId</em>.
/// </summary>
public class AttendanceReportServiceTests : IAsyncDisposable
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    private readonly DatabaseFixture _fixture = new();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private async Task<List<MemberAttendanceStat>> GetStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        await using var db = _fixture.NewContext();
        return await new AttendanceReportService(db).GetMemberStatsAsync(from ?? From, to ?? To);
    }

    private static DateTime Day(int month, int day) => new(2026, month, day, 18, 0, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------- zkoušky

    [Fact]
    public async Task Zkousky_se_pocitaji_z_radku_bez_EventId()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(member.Id, Day(1, 9), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id, Day(1, 16), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id, Day(1, 23), AttendanceStatus.No));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal(3, stat.TotalRehearsals);
        Assert.Equal(2, stat.AttendedRehearsals);
        Assert.Equal(66.7, stat.RehearsalRate);
    }

    /// <summary>
    /// Celkový počet zkoušek je počet <em>různých dat</em>, ne počet řádků — jinak by
    /// se pětičlenné ansámblu jedna zkouška počítala pětkrát.
    /// </summary>
    [Fact]
    public async Task Celkovy_pocet_zkousek_je_pocet_ruznych_dat_ne_radku()
    {
        await using var db = _fixture.NewContext();
        var first = await TestData.SeedMemberAsync(db, "prvni");
        var second = await TestData.SeedMemberAsync(db, "druhy");
        var third = await TestData.SeedMemberAsync(db, "treti");

        // Jedna zkouška, tři docházkové řádky.
        foreach (var id in new[] { first.Id, second.Id, third.Id })
            db.Attendances.Add(TestData.RehearsalAttendance(id, Day(2, 6), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        var stats = await GetStatsAsync();

        Assert.Equal(3, stats.Count);
        Assert.All(stats, s => Assert.Equal(1, s.TotalRehearsals));
    }

    [Fact]
    public async Task Zkousky_ve_stejny_den_ale_v_jinou_hodinu_se_pocitaji_jako_jedna()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(member.Id,
                new DateTime(2026, 3, 6, 17, 0, 0, DateTimeKind.Utc), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id,
                new DateTime(2026, 3, 6, 20, 0, 0, DateTimeKind.Utc), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        // Distinct jde přes a.Date.Date, takže obě hodiny spadnou do jednoho dne.
        Assert.Equal(1, stat.TotalRehearsals);
    }

    // ---------------------------------------------------------------- akce

    [Fact]
    public async Task Akce_se_pocitaji_z_radku_s_vyplnenym_EventId()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var first = TestData.Event("Ples");
        var second = TestData.Event("Festival");
        db.Events.AddRange(first, second);
        await db.SaveChangesAsync();

        db.Attendances.AddRange(
            TestData.ActionAttendance(member.Id, first.Id, Day(2, 14), AttendanceStatus.Yes),
            TestData.ActionAttendance(member.Id, second.Id, Day(7, 4), AttendanceStatus.No));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal(2, stat.TotalActions);
        Assert.Equal(1, stat.AttendedActions);
        Assert.Equal(50.0, stat.ActionRate);
        Assert.Equal(0, stat.TotalRehearsals);
    }

    [Fact]
    public async Task Zkousky_a_akce_se_navzajem_nemichaji()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var ev = TestData.Event();
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        db.Attendances.AddRange(
            TestData.RehearsalAttendance(member.Id, Day(1, 9), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id, Day(1, 16), AttendanceStatus.Yes),
            TestData.ActionAttendance(member.Id, ev.Id, Day(6, 1), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal(2, stat.TotalRehearsals);
        Assert.Equal(2, stat.AttendedRehearsals);
        Assert.Equal(1, stat.TotalActions);
        Assert.Equal(1, stat.AttendedActions);
    }

    // ---------------------------------------------------------------- stav Maybe

    /// <summary>
    /// Do docházky se počítá jen <see cref="AttendanceStatus.Yes"/>.
    /// <see cref="AttendanceStatus.Maybe"/> je stav pro plánování, ne pro účast.
    /// </summary>
    [Fact]
    public async Task Stav_maybe_se_nepocita_jako_ucast_ale_zkousku_zahrne_do_celku()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(member.Id, Day(1, 9), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id, Day(1, 16), AttendanceStatus.Maybe),
            TestData.RehearsalAttendance(member.Id, Day(1, 23), AttendanceStatus.No));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal(3, stat.TotalRehearsals);
        Assert.Equal(1, stat.AttendedRehearsals);
    }

    // ---------------------------------------------------------------- filtry a hranice

    [Fact]
    public async Task Clenove_se_skrytou_dochazkou_se_do_statistik_nedostanou()
    {
        await using var db = _fixture.NewContext();
        var visible = await TestData.SeedMemberAsync(db, "videt");
        var hidden = await TestData.SeedMemberAsync(db, "skryty", isAttendanceVisible: false);
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(visible.Id, Day(1, 9), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(hidden.Id, Day(1, 9), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        var stats = await GetStatsAsync();

        var stat = Assert.Single(stats);
        Assert.Equal(visible.Id, stat.MemberId);
    }

    [Fact]
    public async Task Dochazka_mimo_obdobi_se_nepocita()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(member.Id, Day(6, 5), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id,
                new DateTime(2025, 6, 5, 18, 0, 0, DateTimeKind.Utc), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync(
            from: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(1, stat.TotalRehearsals);
    }

    [Fact]
    public async Task Prazdne_obdobi_vrati_prazdny_seznam_a_ne_deleni_nulou()
    {
        await using var db = _fixture.NewContext();
        await TestData.SeedMemberAsync(db);

        var stats = await GetStatsAsync();

        Assert.Empty(stats);
    }

    [Fact]
    public async Task Clen_bez_ani_jedne_ucasti_ma_nulovou_ucast_a_ne_NaN()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.Add(TestData.RehearsalAttendance(member.Id, Day(1, 9), AttendanceStatus.No));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal(0, stat.AttendedRehearsals);
        Assert.Equal(0.0, stat.RehearsalRate);
        // Akce v období žádné nejsou — dělitel je nula a musí být ošetřený.
        Assert.Equal(0, stat.TotalActions);
        Assert.Equal(0.0, stat.ActionRate);
    }

    [Fact]
    public async Task Vysledek_je_serazeny_podle_ucasti_na_zkouskach_klesajicne()
    {
        await using var db = _fixture.NewContext();
        var weak = await TestData.SeedMemberAsync(db, "slaby");
        var strong = await TestData.SeedMemberAsync(db, "silny");
        var middle = await TestData.SeedMemberAsync(db, "prostredni");

        var days = new[] { Day(1, 9), Day(1, 16), Day(1, 23), Day(1, 30) };
        // silný 4/4, prostřední 2/4, slabý 1/4
        var plan = new (int MemberId, int Attended)[] { (strong.Id, 4), (middle.Id, 2), (weak.Id, 1) };
        foreach (var (memberId, attended) in plan)
            for (var i = 0; i < days.Length; i++)
                db.Attendances.Add(TestData.RehearsalAttendance(memberId, days[i],
                    i < attended ? AttendanceStatus.Yes : AttendanceStatus.No));
        await db.SaveChangesAsync();

        var stats = await GetStatsAsync();

        Assert.Equal([strong.Id, middle.Id, weak.Id], stats.Select(s => s.MemberId));
        Assert.Equal([100.0, 50.0, 25.0], stats.Select(s => s.RehearsalRate));
    }

    [Fact]
    public async Task Statistika_nese_cele_jmeno_clena()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.Add(TestData.RehearsalAttendance(member.Id, Day(1, 9), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal("Jan Novak", stat.FullName);
    }

    [Fact]
    public async Task Ucast_se_zaokrouhluje_na_jedno_desetinne_misto()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        // 1 ze 3 = 33,333… %
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(member.Id, Day(1, 9), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(member.Id, Day(1, 16), AttendanceStatus.No),
            TestData.RehearsalAttendance(member.Id, Day(1, 23), AttendanceStatus.No));
        await db.SaveChangesAsync();

        var stat = Assert.Single(await GetStatsAsync());

        Assert.Equal(33.3, stat.RehearsalRate);
    }
}
