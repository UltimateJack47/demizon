using Demizon.Common.Exceptions;
using Demizon.Common;
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

        ResultAssert.Ok(saved);
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

        ResultAssert.Failed(await Attendances(db).CreateOrUpdateAsync(orphan));
    }

    /// <summary>
    /// Na vložené cestě je předaná entita ta trackovaná, takže jí EF po uložení
    /// dopíše vygenerovaný klíč. Na tom stojí oprava osiřelých událostí v Google
    /// Calendaru: <c>MemberAttendance.razor.cs</c> potřebuje ID nové docházky,
    /// aby k ní mohl zapsat ID vytvořené události. Kdyby služba někdy začala
    /// kopírovat do jiné instance, tenhle test to zachytí — jinak by se to
    /// projevilo až neodstranitelnou událostí v cizím kalendáři.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_vyplni_Id_na_predane_entite()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var attendance = TestData.RehearsalAttendance(member.Id, Day(5, 1), AttendanceStatus.Yes);
        Assert.Equal(0, attendance.Id);

        ResultAssert.Ok(await Attendances(db).CreateOrUpdateAsync(attendance));

        Assert.NotEqual(0, attendance.Id);
        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Attendances.ToListAsync());
        Assert.Equal(stored.Id, attendance.Id);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_nepresene_Id_pri_neuspechu()
    {
        await using var db = _fixture.NewContext();
        var orphan = TestData.RehearsalAttendance(memberId: 99999, Day(5, 1), AttendanceStatus.Yes);

        ResultAssert.Failed(await Attendances(db).CreateOrUpdateAsync(orphan));

        // Volající se rozhoduje podle Id != 0, takže po selhání tam nesmí
        // zůstat klíč, který v databázi nic neoznačuje.
        Assert.Equal(0, orphan.Id);
    }

    /// <summary>
    /// Celý cyklus, který plán požaduje k osiřelým událostem: nová docházka →
    /// klíč → dopsání <c>GoogleEventId</c> → přepnutí na „nepřijdu“ ho umí najít
    /// a smazat. Dřív zůstalo <c>model.Id</c> nulové, ID události se nikam
    /// nezapsalo a událost v kalendáři už nešlo odstranit.
    /// </summary>
    [Fact]
    public async Task Nova_dochazka_umi_prijmout_a_pozdeji_zahodit_GoogleEventId()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var service = Attendances(db);
        var created = TestData.RehearsalAttendance(member.Id, Day(5, 1), AttendanceStatus.Yes);
        ResultAssert.Ok(await service.CreateOrUpdateAsync(created));
        var attendanceId = created.Id;
        Assert.NotEqual(0, attendanceId);

        // Zápis ID události k existujícímu řádku docházky.
        await using var writeBack = _fixture.NewContext();
        var loaded = await Attendances(writeBack).GetOneAsync(attendanceId);
        loaded.GoogleEventId = "google-event-abc";
        ResultAssert.Ok(await Attendances(writeBack).CreateOrUpdateAsync(loaded));

        await using var verify = _fixture.NewContext();
        Assert.Equal("google-event-abc",
            (await verify.Attendances.SingleAsync(a => a.Id == attendanceId)).GoogleEventId);

        // Přepnutí na „nepřijdu“: událost se maže a ID se zahazuje.
        await using var clear = _fixture.NewContext();
        var toClear = await Attendances(clear).GetOneAsync(attendanceId);
        Assert.Equal("google-event-abc", toClear.GoogleEventId);
        toClear.Status = AttendanceStatus.No;
        toClear.GoogleEventId = null;
        ResultAssert.Ok(await Attendances(clear).CreateOrUpdateAsync(toClear));

        await using var final = _fixture.NewContext();
        var stored = await final.Attendances.SingleAsync(a => a.Id == attendanceId);
        Assert.Null(stored.GoogleEventId);
        Assert.Equal(AttendanceStatus.No, stored.Status);
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
        ResultAssert.Ok(await Attendances(db).DeleteAsync(attendance.Id));

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Attendances.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_neexistujici_dochazky_vrati_false()
    {
        await using var db = _fixture.NewContext();

        ResultAssert.Failed(await Attendances(db).DeleteAsync(4242));
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

        ResultAssert.Ok(await Events(db).CreateAsync(TestData.Event("Hody")));

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
    /// Dřív se tady chování rozcházelo: <c>AttendanceService.DeleteAsync</c> vracela
    /// <c>false</c>, ale <c>EventService.DeleteAsync</c> na neexistující akci
    /// <b>hodila výjimku</b>, protože kontrola běžela před try blokem. Volající to
    /// musel vědět u každé služby zvlášť.
    /// <para>
    /// S <c>Result</c> je to sjednocené: chybějící řádek je <c>NotFound</c>, ne
    /// výjimka. Controller z toho udělá HTTP 404, aniž by rozebíral text chyby.
    /// </para>
    /// </summary>
    [Fact]
    public async Task DeleteAsync_neexistujici_akce_vrati_NotFound()
    {
        await using var db = _fixture.NewContext();

        ResultAssert.Failed(await Events(db).DeleteAsync(4242), ResultErrorKind.NotFound);
    }

    /// <summary>
    /// Stejná otázka u ostatních služeb — smyslem je, aby se odpověď nelišila
    /// podle toho, na kterou službu se volající zeptá.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_neexistujici_dochazky_vrati_taky_NotFound()
    {
        await using var db = _fixture.NewContext();

        ResultAssert.Failed(await Attendances(db).DeleteAsync(4242), ResultErrorKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_akce_smaze_zaznam()
    {
        await using var seed = _fixture.NewContext();
        var ev = TestData.Event();
        seed.Events.Add(ev);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        ResultAssert.Ok(await Events(db).DeleteAsync(ev.Id));

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
