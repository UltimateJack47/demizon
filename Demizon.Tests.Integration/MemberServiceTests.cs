using Demizon.Common.Exceptions;
using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

public class MemberServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "admin");

    private MemberService CreateService(DemizonContext db) => new(db, NullLogger<MemberService>.Instance);

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    // ---------------------------------------------------------------- soft delete

    /// <summary>
    /// Soft delete je v <c>DemizonContext</c> implementovaný globálním query filtrem
    /// (<c>Member.DeletedAt == null</c>). Historie docházky musí zůstat, člen se jen skryje.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_nastavi_DeletedAt_a_neodstrani_radek()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);

        var deleted = await CreateService(db).DeleteAsync(member.Id);

        Assert.True(deleted);

        await using var verify = _fixture.NewContext();
        var stillThere = await verify.Members.IgnoreQueryFilters()
            .SingleAsync(m => m.Id == member.Id);
        Assert.NotNull(stillThere.DeletedAt);
    }

    [Fact]
    public async Task Smazany_clen_zmizi_z_bezneho_dotazu_ale_zustane_pod_IgnoreQueryFilters()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        await CreateService(db).DeleteAsync(member.Id);

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Members.Where(m => m.Id == member.Id).ToListAsync());
        Assert.Single(await verify.Members.IgnoreQueryFilters().Where(m => m.Id == member.Id).ToListAsync());
    }

    [Fact]
    public async Task Dochazka_smazaneho_clena_zustane_v_databazi()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.Add(TestData.RehearsalAttendance(member.Id,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await CreateService(db).DeleteAsync(member.Id);

        await using var verify = _fixture.NewContext();
        Assert.Single(await verify.Attendances.Where(a => a.MemberId == member.Id).ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_neexistujiciho_clena_vrati_false()
    {
        await using var db = _fixture.NewContext();

        Assert.False(await CreateService(db).DeleteAsync(999));
    }

    [Fact]
    public async Task GetAll_nevraci_smazane_cleny()
    {
        await using var db = _fixture.NewContext();
        var kept = await TestData.SeedMemberAsync(db, "zustava");
        var removed = await TestData.SeedMemberAsync(db, "mizi");
        await CreateService(db).DeleteAsync(removed.Id);

        await using var verify = _fixture.NewContext();
        var all = await CreateService(verify).GetAll().ToListAsync();

        Assert.Equal([kept.Id], all.Select(m => m.Id));
    }

    // ---------------------------------------------------------------- čtení

    [Fact]
    public async Task GetOneAsync_neexistujiciho_clena_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => CreateService(db).GetOneAsync(12345));
    }

    [Fact]
    public async Task GetOneByLogin_najde_clena_podle_loginu()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db, "jack");

        await using var verify = _fixture.NewContext();
        var found = CreateService(verify).GetOneByLogin("jack");

        Assert.NotNull(found);
        Assert.Equal(member.Id, found.Id);
    }

    [Theory]
    [InlineData("neexistuje")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetOneByLogin_neznameho_loginu_vrati_null(string? login)
    {
        await using var db = _fixture.NewContext();
        await TestData.SeedMemberAsync(db, "jack");

        await using var verify = _fixture.NewContext();
        Assert.Null(CreateService(verify).GetOneByLogin(login));
    }

    // ---------------------------------------------------------------- Google Calendar

    /// <summary>
    /// Invariant z <c>MemberService.UpdateAsync</c>: Google tokeny spravují výhradně
    /// metody Connect/Disconnect. Kdyby je šlo přepsat běžným uloženým formulářem,
    /// jedno uložení profilu by členovi odpojilo kalendář.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_neprepise_Google_tokeny()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);
        await CreateService(seed).ConnectGoogleCalendarAsync(member.Id, "refresh-token-123", "primary");

        await using var db = _fixture.NewContext();
        // Formulář o Google tokenech nic neví, takže je posílá prázdné.
        var fromForm = TestData.Member(login: "tester", name: "Novy", surname: "Prijmeni");
        fromForm.Id = member.Id;
        fromForm.GoogleRefreshToken = null;
        fromForm.GoogleCalendarId = null;

        await CreateService(db).UpdateAsync(member.Id, fromForm);

        await using var verify = _fixture.NewContext();
        var updated = await verify.Members.SingleAsync(m => m.Id == member.Id);
        Assert.Equal("Novy", updated.Name);
        Assert.Equal("refresh-token-123", updated.GoogleRefreshToken);
        Assert.Equal("primary", updated.GoogleCalendarId);
        Assert.NotNull(updated.GoogleConnectedAt);
    }

    [Fact]
    public async Task UpdateAsync_neexistujiciho_clena_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CreateService(db).UpdateAsync(999, TestData.Member()));
    }

    [Fact]
    public async Task ConnectGoogleCalendarAsync_ulozi_token_kalendar_a_cas_propojeni()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var before = DateTime.UtcNow;

        await CreateService(db).ConnectGoogleCalendarAsync(member.Id, "token", "cal-id");

        await using var verify = _fixture.NewContext();
        var updated = await verify.Members.SingleAsync(m => m.Id == member.Id);
        Assert.Equal("token", updated.GoogleRefreshToken);
        Assert.Equal("cal-id", updated.GoogleCalendarId);
        Assert.InRange(updated.GoogleConnectedAt!.Value, before.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task DisconnectGoogleCalendarAsync_vycisti_vsechna_tri_pole()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        await CreateService(db).ConnectGoogleCalendarAsync(member.Id, "token", "cal-id");

        await CreateService(db).DisconnectGoogleCalendarAsync(member.Id);

        await using var verify = _fixture.NewContext();
        var updated = await verify.Members.SingleAsync(m => m.Id == member.Id);
        Assert.Null(updated.GoogleRefreshToken);
        Assert.Null(updated.GoogleCalendarId);
        Assert.Null(updated.GoogleConnectedAt);
    }

    [Fact]
    public async Task ConnectGoogleCalendarAsync_neexistujiciho_clena_hodi_EntityNotFoundException()
    {
        await using var db = _fixture.NewContext();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CreateService(db).ConnectGoogleCalendarAsync(999, "t", "c"));
    }

    // ---------------------------------------------------------------- zápis

    [Fact]
    public async Task CreateAsync_ulozi_clena_a_vrati_true()
    {
        await using var db = _fixture.NewContext();

        var created = await CreateService(db).CreateAsync(TestData.Member(login: "novy"));

        Assert.True(created);
        await using var verify = _fixture.NewContext();
        Assert.NotNull(await verify.Members.SingleOrDefaultAsync(m => m.Login == "novy"));
    }

    [Fact]
    public async Task CreateAsync_pri_chybe_vrati_false_a_nepropusti_vyjimku()
    {
        await using var db = _fixture.NewContext();
        // Name je v modelu required — uložení musí selhat, ale služba chybu jen zaloguje.
        var invalid = TestData.Member();
        invalid.Name = null!;

        Assert.False(await CreateService(db).CreateAsync(invalid));
    }
}
