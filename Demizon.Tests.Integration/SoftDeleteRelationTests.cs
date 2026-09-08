using Demizon.Core.Services.Authentication;
using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// <c>Member</c> má globální query filtr (<c>DeletedAt == null</c>) a je zároveň
/// povinným koncem relací <c>Attendance</c>, <c>RefreshToken</c>, <c>DeviceToken</c>
/// a <c>PushSubscription</c>. EF na to při startu upozorňuje varováním 10622:
/// dotaz, který se přes takovou navigaci připojí, může řádky <b>ztratit</b>,
/// protože povinná navigace se překládá na INNER JOIN.
/// </summary>
/// <remarks>
/// Testy tu popisují skutečné chování, ne přání. Jsou napsané tak, aby zčervenaly,
/// když se to chování změní — ať už opravou filtrů, nebo upgradem EF.
/// </remarks>
public class SoftDeleteRelationTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "admin");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private static readonly DateTime Friday = new(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc);

    private async Task<int> SeedDeletedMemberWithAttendanceAsync()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        db.Attendances.Add(TestData.RehearsalAttendance(member.Id, Friday, AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await using var delete = _fixture.NewContext();
        ResultAssert.Ok(await new MemberService(delete, NullLogger<MemberService>.Instance)
            .DeleteAsync(member.Id));
        return member.Id;
    }

    // ------------------------------------------------------------- historie docházky

    [Fact]
    public async Task Dochazka_smazaneho_clena_zustava_dohledatelna()
    {
        var memberId = await SeedDeletedMemberWithAttendanceAsync();

        await using var db = _fixture.NewContext();
        var attendances = await db.Attendances.Where(a => a.MemberId == memberId).ToListAsync();

        // Attendance vlastní filtr nemá, takže samotný dotaz řádek vidí.
        Assert.Single(attendances);
    }

    /// <summary>
    /// Tohle je ta past z varování 10622. <c>Attendance.Member</c> je povinná
    /// navigace, takže <c>Include</c> generuje INNER JOIN a filtr na
    /// <c>Member</c> řádek docházky odstraní — přesto že v tabulce je.
    /// </summary>
    [Fact]
    public async Task Include_Member_zahodi_dochazku_smazaneho_clena()
    {
        var memberId = await SeedDeletedMemberWithAttendanceAsync();

        await using var db = _fixture.NewContext();
        var withInclude = await db.Attendances
            .Include(a => a.Member)
            .Where(a => a.MemberId == memberId)
            .ToListAsync();
        var withoutInclude = await db.Attendances
            .Where(a => a.MemberId == memberId)
            .ToListAsync();

        Assert.Single(withoutInclude);
        Assert.Empty(withInclude);
    }

    /// <summary>
    /// Obejít to jde <c>IgnoreQueryFilters</c>, ale je to vědomé rozhodnutí —
    /// dotaz pak vydá i členy, které má zbytek aplikace za smazané.
    /// </summary>
    [Fact]
    public async Task IgnoreQueryFilters_dochazku_smazaneho_clena_vrati()
    {
        var memberId = await SeedDeletedMemberWithAttendanceAsync();

        await using var db = _fixture.NewContext();
        var rows = await db.Attendances
            .IgnoreQueryFilters()
            .Include(a => a.Member)
            .Where(a => a.MemberId == memberId)
            .ToListAsync();

        var row = Assert.Single(rows);
        Assert.NotNull(row.Member);
        Assert.NotNull(row.Member.DeletedAt);
    }

    // --------------------------------------------------------------- bezpečnost

    /// <summary>
    /// Opačný směr filtru je tady žádoucí: smazanému členovi nesmí projít obnova
    /// tokenu, jinak by se odhlášený účet dal držet naživu do expirace refresh
    /// tokenu (30 dní).
    /// </summary>
    [Fact]
    public async Task Refresh_token_smazaneho_clena_neprojde_pres_MemberService()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var raw = await new RefreshTokenService(db)
            .CreateAsync(member.Id, expirationDays: 30);

        await using var delete = _fixture.NewContext();
        ResultAssert.Ok(await new MemberService(delete, NullLogger<MemberService>.Instance)
            .DeleteAsync(member.Id));

        await using var verify = _fixture.NewContext();
        // Samotný token zůstává platný — na filtru Member neleží.
        var memberIdFromToken = await new RefreshTokenService(verify)
            .ValidateAsync(raw);
        Assert.Equal(member.Id, memberIdFromToken);

        // Zachytí se to až o krok dál: AuthController po validaci sahá na člena
        // a ten je filtrem skrytý, takže obnova skončí chybou, ne novým tokenem.
        await using var lookup = _fixture.NewContext();
        await Assert.ThrowsAsync<Common.Exceptions.EntityNotFoundException>(
            () => new MemberService(lookup, NullLogger<MemberService>.Instance)
                .GetOneAsync(member.Id));
    }
}
