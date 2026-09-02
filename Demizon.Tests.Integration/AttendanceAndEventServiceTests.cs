using Demizon.Common.Exceptions;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

public class AttendanceAndEventServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "tester");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private static AttendanceService Attendances(DemizonContext db) =>
        new(db, NullLogger<AttendanceService>.Instance);

    private static EventService Events(DemizonContext db) =>
        new(db, NullLogger<EventService>.Instance);

    private static DateTime Day(int month, int day) => new(2026, month, day, 18, 0, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------- AttendanceService

    [Fact]
    public async Task CreateOrUpdateAsync_vlozi_novy_zaznam()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);

        var saved = await Attendances(db).CreateOrUpdateAsync(
            TestData.RehearsalAttendance(member.Id, Day(5, 1), AttendanceStatus.Yes));

        Assert.True(saved);
        await using var verify = _fixture.NewContext();
        Assert.Single(await verify.Attendances.ToListAsync());
    }

    [Fact]
    public async Task CreateOrUpdateAsync_prepise_existujici_zaznam_podle_Id()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);
        var attendance = TestData.RehearsalAttendance(member.Id, Day(5, 1), AttendanceStatus.No);
        seed.Attendances.Add(attendance);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        await Attendances(db).CreateOrUpdateAsync(new Attendance
        {
            Id = attendance.Id,
            MemberId = member.Id,
            EventId = null,
            Date = Day(5, 1),
            Status = AttendanceStatus.Yes,
            Comment = "Nakonec přijdu"
        });

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Attendances.ToListAsync());
        Assert.Equal(attendance.Id, stored.Id);
        Assert.Equal(AttendanceStatus.Yes, stored.Status);
        Assert.Equal("Nakonec přijdu", stored.Comment);
    }

    /// <summary>
    /// <c>LastUpdated</c> nastavuje služba sama, ne volající — jinak by šlo poslat
    /// libovolný čas a znehodnotit auditní stopu poslední změny.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_prepise_LastUpdated_vlastnim_casem()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var attendance = TestData.RehearsalAttendance(member.Id, Day(5, 1), AttendanceStatus.Yes);
        attendance.LastUpdated = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await Attendances(db).CreateOrUpdateAsync(attendance);

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Attendances.ToListAsync());
        Assert.True(stored.LastUpdated > new DateTime(2020, 1, 1),
            $"LastUpdated má být čas uložení, ne {stored.LastUpdated:o}");
    }

    [Fact]
    public async Task CreateOrUpdateAsync_pri_chybe_vrati_false_a_nepropusti_vyjimku()
    {
        await using var db = _fixture.NewContext();
        // MemberId ukazuje nikam — musí selhat na FK, ale služba chybu jen zaloguje.
        var orphan = TestData.RehearsalAttendance(memberId: 99999, Day(5, 1), AttendanceStatus.Yes);

        Assert.False(await Attendances(db).CreateOrUpdateAsync(orphan));
    }

    [Fact]
    public async Task GetOneAsync_neexistujici_dochazky_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Attendances(db).GetOneAsync(4242));
    }

    [Fact]
    public async Task DeleteAsync_dochazky_smaze_radek_natvrdo()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);
        var attendance = TestData.RehearsalAttendance(member.Id, Day(5, 1), AttendanceStatus.Yes);
        seed.Attendances.Add(attendance);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        Assert.True(await Attendances(db).DeleteAsync(attendance.Id));

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Attendances.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_neexistujici_dochazky_vrati_false()
    {
        await using var db = _fixture.NewContext();

        Assert.False(await Attendances(db).DeleteAsync(4242));
    }

    [Fact]
    public async Task GetMemberAttendancesAsync_filtruje_podle_clena_i_obdobi()
    {
        await using var db = _fixture.NewContext();
        var mine = await TestData.SeedMemberAsync(db, "ja");
        var other = await TestData.SeedMemberAsync(db, "nekdo-jiny");
        db.Attendances.AddRange(
            TestData.RehearsalAttendance(mine.Id, Day(3, 6), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(mine.Id, Day(9, 4), AttendanceStatus.Yes),
            TestData.RehearsalAttendance(other.Id, Day(3, 6), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var result = await Attendances(verify)
            .GetMemberAttendancesAsync(mine.Id, Day(3, 1), Day(3, 31));

        var single = Assert.Single(result);
        Assert.Equal(mine.Id, single.MemberId);
        Assert.Equal(Day(3, 6), single.Date);
    }

    [Fact]
    public async Task GetMembersAttendancesAsync_vrati_dochazku_vsech_zadanych_clenu()
    {
        await using var db = _fixture.NewContext();
        var first = await TestData.SeedMemberAsync(db, "prvni");
        var second = await TestData.SeedMemberAsync(db, "druhy");
        var third = await TestData.SeedMemberAsync(db, "treti");
        foreach (var id in new[] { first.Id, second.Id, third.Id })
            db.Attendances.Add(TestData.RehearsalAttendance(id, Day(3, 6), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var result = await Attendances(verify)
            .GetMembersAttendancesAsync([first.Id, third.Id], Day(3, 1), Day(3, 31));

        Assert.Equal([first.Id, third.Id], result.Select(a => a.MemberId).Order());
    }

    [Fact]
    public async Task GetMembersAttendancesAsync_s_prazdnym_seznamem_vrati_prazdny_vysledek()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.Add(TestData.RehearsalAttendance(member.Id, Day(3, 6), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        Assert.Empty(await Attendances(verify).GetMembersAttendancesAsync([], Day(3, 1), Day(3, 31)));
    }

    // ---------------------------------------------------------------- EventService

    [Fact]
    public async Task CreateAsync_akce_ulozi_zaznam()
    {
        await using var db = _fixture.NewContext();

        Assert.True(await Events(db).CreateAsync(TestData.Event("Hody")));

        await using var verify = _fixture.NewContext();
        Assert.Equal("Hody", (await verify.Events.SingleAsync()).Name);
    }

    [Fact]
    public async Task GetOneAsync_neexistujici_akce_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Events(db).GetOneAsync(4242));
    }

    [Fact]
    public async Task UpdateAsync_akce_prepise_pole()
    {
        await using var seed = _fixture.NewContext();
        var ev = TestData.Event("Původní");
        seed.Events.Add(ev);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var updated = TestData.Event("Nový název");
        updated.Id = ev.Id;
        updated.Place = "Nové místo";
        await Events(db).UpdateAsync(ev.Id, updated);

        await using var verify = _fixture.NewContext();
        var stored = await verify.Events.SingleAsync(e => e.Id == ev.Id);
        Assert.Equal("Nový název", stored.Name);
        Assert.Equal("Nové místo", stored.Place);
    }

    /// <summary>
    /// Ostrá hrana v API: <c>UpdateAsync</c> bere <c>id</c> i celou entitu a mlčky
    /// předpokládá, že <c>entity.Id == id</c>. Kopíruje totiž <em>všechny</em> hodnoty
    /// včetně klíče, takže entita s <c>Id == 0</c> (typický výstup mapování z DTO)
    /// skončí výjimkou, ne uložením. Všichni současní volající si entitu nejdřív načtou
    /// přes <c>GetOneAsync</c>, takže je to v pořádku — test tu podmínku jen pojmenovává.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_akce_s_neshodnym_Id_selze()
    {
        await using var seed = _fixture.NewContext();
        var ev = TestData.Event();
        seed.Events.Add(ev);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var withoutId = TestData.Event("Bez Id");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Events(db).UpdateAsync(ev.Id, withoutId));
    }

    [Fact]
    public async Task UpdateAsync_neexistujici_akce_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Events(db).UpdateAsync(4242, TestData.Event()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetCancelledAsync_prepne_priznak_zruseni(bool isCancelled)
    {
        await using var seed = _fixture.NewContext();
        var ev = TestData.Event();
        ev.IsCancelled = !isCancelled;
        seed.Events.Add(ev);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        await Events(db).SetCancelledAsync(ev.Id, isCancelled);

        await using var verify = _fixture.NewContext();
        Assert.Equal(isCancelled, (await verify.Events.SingleAsync(e => e.Id == ev.Id)).IsCancelled);
    }

    [Fact]
    public async Task SetCancelledAsync_neexistujici_akce_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Events(db).SetCancelledAsync(4242, true));
    }

    /// <summary>
    /// Odlišné chování od <c>AttendanceService.DeleteAsync</c>, které vrací <c>false</c>:
    /// <c>EventService.DeleteAsync</c> na neexistující akci <b>hodí výjimku</b>, protože
    /// kontrola běží před try blokem. Test tu nesrovnalost pojmenovává, aby ji volající
    /// nemusel hádat.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_neexistujici_akce_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => Events(db).DeleteAsync(4242));
    }

    [Fact]
    public async Task DeleteAsync_akce_smaze_zaznam()
    {
        await using var seed = _fixture.NewContext();
        var ev = TestData.Event();
        seed.Events.Add(ev);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        Assert.True(await Events(db).DeleteAsync(ev.Id));

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Events.ToListAsync());
    }

    [Fact]
    public async Task GetAll_vraci_vsechny_akce_vcetne_zrusenych()
    {
        await using var db = _fixture.NewContext();
        var active = TestData.Event("Aktivní");
        var cancelled = TestData.Event("Zrušená");
        cancelled.IsCancelled = true;
        db.Events.AddRange(active, cancelled);
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        // Na akcích není soft delete ani filtr na zrušení — filtruje se až v UI.
        Assert.Equal(2, await Events(verify).GetAll().CountAsync());
    }
}
