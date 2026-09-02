using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.File;
using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// Služby, které výjimku ze zápisu spolykají a vrátí <c>false</c>, musí po sobě uklidit
/// change tracker.
/// </summary>
/// <remarks>
/// EF Core při výjimce ze <c>SaveChangesAsync</c> tracker <b>nevrací</b> — entita zůstane
/// <see cref="EntityState.Added"/>. A protože je kontext v Blazor Serveru scoped na celý
/// okruh, přehraje se ta rozpracovaná změna při každém dalším uložení v téže session.
/// <para>
/// Bez úklidu je vrácené <c>false</c> pro volajícího nepoužitelné: neznamená „neuložilo se“,
/// ale „neuložilo se <em>teď</em>“ — a klidně se uloží později, u nesouvisející akce.
/// </para>
/// </remarks>
public class ChangeTrackerRecoveryTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "admin");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>Člen s <c>Name = null</c> narazí na NOT NULL, takže zápis spolehlivě selže.</summary>
    private static Member InvalidMember(string login)
    {
        var member = TestData.Member(login: login);
        member.Name = null!;
        return member;
    }

    // ---------------------------------------------------------------- Member

    [Fact]
    public async Task Neuspesne_vytvoreni_clena_nezustane_v_change_trackeru()
    {
        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        Assert.False(await service.CreateAsync(InvalidMember("vadny")));

        Assert.DoesNotContain(db.ChangeTracker.Entries<Member>(), e => e.State == EntityState.Added);
    }

    /// <summary>
    /// Jádro chyby: bez úklidu by se u druhého — platného — uložení pokusil vložit
    /// i ten první vadný člen, celé uložení by znovu selhalo a admin by nikdy
    /// nedokázal formulář uložit, ať by v něm opravil cokoli.
    /// </summary>
    [Fact]
    public async Task Po_neuspesnem_vytvoreni_clena_projde_dalsi_pokus()
    {
        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        Assert.False(await service.CreateAsync(InvalidMember("vadny")));
        Assert.True(await service.CreateAsync(TestData.Member(login: "opraveny")));

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Members.ToListAsync());
        Assert.Equal("opraveny", stored.Login);
    }

    // ---------------------------------------------------------------- File

    [Fact]
    public async Task Neuspesne_ulozeni_souboru_nezustane_v_change_trackeru()
    {
        await using var db = _fixture.NewContext();
        var service = new FileService(db, NullLogger<FileService>.Instance);

        // Path je v modelu required.
        Assert.False(await service.CreateAsync(new Dal.Entities.File
        {
            Path = null!,
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 1
        }));

        Assert.DoesNotContain(db.ChangeTracker.Entries<Dal.Entities.File>(),
            e => e.State == EntityState.Added);
    }

    /// <summary>
    /// Přesně scénář z nahrávání více fotek: u první selže zápis, druhá projde.
    /// Bez úklidu by druhé uložení vložilo <b>obě</b>, takže by se první nahlásila
    /// jako neúspěšná, přesto že v databázi skončila.
    /// </summary>
    [Fact]
    public async Task Po_neuspesnem_ulozeni_souboru_se_ulozi_jen_ten_dalsi()
    {
        await using var db = _fixture.NewContext();
        var service = new FileService(db, NullLogger<FileService>.Instance);

        Assert.False(await service.CreateAsync(new Dal.Entities.File
        {
            Path = null!,
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 1
        }));
        Assert.True(await service.CreateAsync(new Dal.Entities.File
        {
            Path = "db-stored",
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 2
        }));

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Files.ToListAsync());
        Assert.Equal("db-stored", stored.Path);
    }

    // ---------------------------------------------------------------- Event

    [Fact]
    public async Task Po_neuspesnem_vytvoreni_akce_projde_dalsi_pokus()
    {
        await using var db = _fixture.NewContext();
        var service = new EventService(db, NullLogger<EventService>.Instance);

        var invalid = TestData.Event();
        invalid.Name = null!;

        Assert.False(await service.CreateAsync(invalid));
        Assert.True(await service.CreateAsync(TestData.Event("Platná akce")));

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Events.ToListAsync());
        Assert.Equal("Platná akce", stored.Name);
    }

    // ---------------------------------------------------------------- Attendance

    [Fact]
    public async Task Po_neuspesnem_zapisu_dochazky_projde_dalsi_pokus()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var service = new AttendanceService(db, NullLogger<AttendanceService>.Instance);
        var day = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc);

        // MemberId ukazuje nikam — porušení FK.
        Assert.False(await service.CreateOrUpdateAsync(
            TestData.RehearsalAttendance(memberId: 99999, day, AttendanceStatus.Yes)));
        Assert.True(await service.CreateOrUpdateAsync(
            TestData.RehearsalAttendance(member.Id, day, AttendanceStatus.Yes)));

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Attendances.ToListAsync());
        Assert.Equal(member.Id, stored.MemberId);
    }

    // ---------------------------------------------------------------- pomůcka samotná

    [Theory]
    [InlineData(EntityState.Added, EntityState.Detached)]
    [InlineData(EntityState.Modified, EntityState.Unchanged)]
    [InlineData(EntityState.Deleted, EntityState.Unchanged)]
    public async Task DiscardPendingChange_prevede_stav_na_ocekavany(
        EntityState from, EntityState expected)
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        db.Entry(tracked).State = from == EntityState.Added ? EntityState.Unchanged : from;

        object target = tracked;
        if (from == EntityState.Added)
        {
            // Added se nasimuluje novou, netrackovanou entitou.
            target = TestData.Member(login: "novy");
            db.Members.Add((Member)target);
        }

        db.DiscardPendingChange(target);

        Assert.Equal(expected, db.Entry(target).State);
    }

    [Fact]
    public async Task DiscardPendingChange_nechava_netrackovanou_entitu_bez_zmeny()
    {
        await using var db = _fixture.NewContext();
        var detached = TestData.Member(login: "nikdy-netrackovany");

        db.DiscardPendingChange(detached);

        Assert.Equal(EntityState.Detached, db.Entry(detached).State);
    }

    [Fact]
    public async Task DiscardPendingChange_zvlada_null()
    {
        await using var db = _fixture.NewContext();

        db.DiscardPendingChange(null);

        Assert.Empty(db.ChangeTracker.Entries());
    }
}
