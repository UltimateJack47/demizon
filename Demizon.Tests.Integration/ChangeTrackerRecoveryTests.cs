using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.File;
using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    // ------------------------------------------------- díry, které cílení na jednu entitu mělo

    /// <summary>
    /// Cílit úklid na jednu entitu nestačí: <c>AddAsync(member)</c> u člena s fotkou
    /// nastraží jako <c>Added</c> i tu fotku, takže odpojení samotného člena ji nechá
    /// v trackeru a vložila by se s příštím uložením. Přesně tenhle graf ukládá
    /// <c>MemberForm.razor</c> při zakládání člena s profilovkou.
    /// </summary>
    [Fact]
    public async Task Neuspesne_ulozeni_grafu_nenecha_v_trackeru_ani_navazane_entity()
    {
        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        var member = InvalidMember("vadny-s-fotkou");
        member.Photos.Add(new Dal.Entities.File
        {
            Path = "db-stored",
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 10
        });

        Assert.False(await service.CreateAsync(member));

        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task Po_neuspesnem_ulozeni_grafu_se_neulozi_osirela_fotka()
    {
        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        var invalid = InvalidMember("vadny-s-fotkou");
        invalid.Photos.Add(new Dal.Entities.File
        {
            Path = "db-stored",
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 10
        });

        Assert.False(await service.CreateAsync(invalid));
        Assert.True(await service.CreateAsync(TestData.Member(login: "opraveny")));

        await using var verify = _fixture.NewContext();
        Assert.Single(await verify.Members.ToListAsync());
        Assert.Empty(await verify.Files.ToListAsync());
    }

    /// <summary>
    /// Na cestě update kopíruje <c>CreateOrUpdateAsync</c> hodnoty do <em>načtené</em>
    /// entity, takže trackovaná je ona, ne ta předaná. Úklid cílený na předanou instanci
    /// by byl no-op a neúspěšný update by se přehrál později.
    /// </summary>
    [Fact]
    public async Task Neuspesny_update_dochazky_nezustane_v_trackeru()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);
        var day = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc);
        var attendance = TestData.RehearsalAttendance(member.Id, day, AttendanceStatus.No);
        seed.Attendances.Add(attendance);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var service = new AttendanceService(db, NullLogger<AttendanceService>.Instance);

        // Stejné Id, ale MemberId ukazuje nikam — update projde přes SetValues
        // na načtenou entitu a selže až na FK.
        Assert.False(await service.CreateOrUpdateAsync(new Attendance
        {
            Id = attendance.Id,
            MemberId = 99999,
            EventId = null,
            Date = day,
            Status = AttendanceStatus.Yes
        }));

        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Attendances.ToListAsync());
        Assert.Equal(member.Id, stored.MemberId);
        Assert.Equal(AttendanceStatus.No, stored.Status);
    }

    /// <summary>
    /// <c>AuditSaveChangesInterceptor</c> přidává <c>AuditLog</c> řádky ještě před
    /// uložením. Když pak uložení selže, nesmí zůstat <c>Added</c> — jinak by se
    /// vložily s příštím uložením jako záznam o změně, která se nikdy nestala.
    /// </summary>
    [Fact]
    public async Task Neuspesne_ulozeni_nenecha_v_trackeru_osirele_audit_radky()
    {
        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        Assert.False(await service.CreateAsync(InvalidMember("vadny")));

        Assert.DoesNotContain(db.ChangeTracker.Entries<AuditLog>(), e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task Po_neuspesnem_ulozeni_neni_v_auditu_zaznam_o_nestale_zmene()
    {
        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        Assert.False(await service.CreateAsync(InvalidMember("vadny")));
        Assert.True(await service.CreateAsync(TestData.Member(login: "opraveny")));

        await using var verify = _fixture.NewContext();
        var audits = await verify.AuditLogs.Where(a => a.EntityType == nameof(Member)).ToListAsync();
        // Právě jeden — za člena, který se skutečně uložil.
        Assert.Single(audits);
    }

    /// <summary>
    /// Zahazuje se i hodnota v paměti, ne jen stav. Kdyby zůstala, další čtení
    /// z téhož kontextu by vydalo člena jako smazaného, přesto že soft delete selhal.
    /// </summary>
    [Fact]
    public async Task Zahozeni_zmeny_vrati_i_hodnoty_v_pameti()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        tracked.Name = "Prepsano";
        tracked.DeletedAt = DateTime.UtcNow;

        db.DiscardPendingChanges();

        Assert.Equal("Jan", tracked.Name);
        Assert.Null(tracked.DeletedAt);
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
    }

    // ------------------------------------------------- pomůcka samotná

    [Fact]
    public async Task DiscardPendingChanges_odpoji_vkladane_entity()
    {
        await using var db = _fixture.NewContext();
        var pending = TestData.Member(login: "novy");
        db.Members.Add(pending);

        db.DiscardPendingChanges();

        Assert.Equal(EntityState.Detached, db.Entry(pending).State);
    }

    [Fact]
    public async Task DiscardPendingChanges_vrati_mazane_entity_do_Unchanged()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        db.Members.Remove(tracked);

        db.DiscardPendingChanges();

        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
    }

    [Fact]
    public async Task DiscardPendingChanges_na_cistem_kontextu_nic_nerozbije()
    {
        await using var db = _fixture.NewContext();

        db.DiscardPendingChanges();

        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    // ------------------------------------------------- metody hlásící výjimkou

    /// <summary>
    /// Metody jako <c>UpdateAsync</c> chybu hlásí výjimkou, ne návratovou hodnotou.
    /// Volající o selhání ví, ale rozpracovaná změna by mu bez úklidu zůstala
    /// v kontextu scoped na celý Blazor okruh.
    /// </summary>
    [Fact]
    public async Task Neuspesna_uprava_clena_nezustane_v_change_trackeru()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var service = new MemberService(db, NullLogger<MemberService>.Instance);

        var invalid = TestData.Member(login: "tester");
        invalid.Id = member.Id;
        invalid.Name = null!;

        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateAsync(member.Id, invalid));

        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    /// <summary>
    /// Jádro nálezu z posledního kola review: bez úklidu vyšel vadný UPDATE ven
    /// s <b>následující nesouvisející</b> operací. Admin, který po neúspěšné úpravě
    /// člena zakládal akci, o tu akci přišel — na chybě, která s ní nemá nic společného.
    /// </summary>
    [Fact]
    public async Task Neuspesna_uprava_neotravi_nasledujici_nesouvisejici_operaci()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var members = new MemberService(db, NullLogger<MemberService>.Instance);
        var events = new EventService(db, NullIogger());

        var invalid = TestData.Member(login: "tester");
        invalid.Id = member.Id;
        invalid.Name = null!;
        await Assert.ThrowsAnyAsync<Exception>(() => members.UpdateAsync(member.Id, invalid));

        // Úplně jiná entita, úplně jiná služba, stejný kontext.
        Assert.True(await events.CreateAsync(TestData.Event("Akce po neúspěchu")));

        await using var verify = _fixture.NewContext();
        var stored = Assert.Single(await verify.Events.ToListAsync());
        Assert.Equal("Akce po neúspěchu", stored.Name);
        // A původní člen zůstal nedotčený.
        Assert.Equal("Jan", (await verify.Members.SingleAsync(m => m.Id == member.Id)).Name);
    }

    [Fact]
    public async Task Neuspesne_zruseni_akce_nezustane_v_change_trackeru()
    {
        await using var seed = _fixture.NewContext();
        var ev = TestData.Event();
        seed.Events.Add(ev);
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var service = new EventService(db, NullIogger());

        // Vynutíme selhání zápisu tím, že akci přepíšeme na neplatný stav.
        var tracked = await db.Events.SingleAsync(e => e.Id == ev.Id);
        tracked.Name = null!;

        await Assert.ThrowsAnyAsync<Exception>(() => service.SetCancelledAsync(ev.Id, true));

        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
        await using var verify = _fixture.NewContext();
        Assert.False((await verify.Events.SingleAsync(e => e.Id == ev.Id)).IsCancelled);
    }

    private static ILogger<EventService> NullIogger() => NullLogger<EventService>.Instance;
}
