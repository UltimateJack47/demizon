using Demizon.Common;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Dance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.Member;
using Demizon.Core.Services.VideoLink;
using Demizon.Dal;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// Kontrakt služeb: <c>CreateAsync</c> ani <c>DeleteAsync</c> **nikdy nevyhodí
/// výjimku**, vždy vrátí <see cref="Result"/>. Volající — Razor stránky
/// i controllery — kolem nich <c>try/catch</c> nemají, protože se na to
/// spoléhají.
/// </summary>
/// <remarks>
/// Vzniklo z regrese, kterou jsem si zavlekl při Result refactoringu:
/// vyhledání entity v <c>DeleteAsync</c> jsem vytáhl <b>mimo</b> <c>try</c>,
/// aby šlo „nenalezeno“ odlišit od chyby zápisu. Tím ale výjimka při čtení
/// (na jednom vCPU s WAL třeba <c>SQLITE_BUSY</c>) přestala být zachycená
/// a prolétla by až do Blazor okruhu — uživateli by místo hlášky zhasla
/// stránka. Nenalezení musí být návratová hodnota, ne výjimka, a čtení musí
/// zůstat v <c>try</c>.
/// <para>
/// Selhání se tu vyvolává tím, že se kontext zavře před voláním služby.
/// Každá operace pak narazí na <c>ObjectDisposedException</c>, tedy na chybu,
/// kterou služba nepředvídá — přesně o ty jde.
/// </para>
/// </remarks>
public class ServiceExceptionContractTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "admin");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private async Task<DemizonContext> DisposedContextAsync()
    {
        var db = _fixture.NewContext();
        await db.DisposeAsync();
        return db;
    }

    [Fact]
    public async Task DeleteAsync_nad_nepouzitelnym_kontextem_nevyhodi_vyjimku()
    {
        var db = await DisposedContextAsync();

        // Každá z těch pěti služeb dřív držela vyhledání entity mimo try.
        var results = new[]
        {
            await new DanceService(db, NullLogger<DanceService>.Instance).DeleteAsync(1),
            await new EventService(db, NullLogger<EventService>.Instance).DeleteAsync(1),
            await new MemberService(db, NullLogger<MemberService>.Instance).DeleteAsync(1),
            await new VideoLinkService(db, NullLogger<VideoLinkService>.Instance).DeleteAsync(1),
            await new AttendanceService(db, NullLogger<AttendanceService>.Instance).DeleteAsync(1),
        };

        Assert.All(results, r =>
        {
            Assert.False(r.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(r.Error), "Neúspěch musí nést text chyby.");
        });
    }

    [Fact]
    public async Task CreateAsync_nad_nepouzitelnym_kontextem_nevyhodi_vyjimku()
    {
        var db = await DisposedContextAsync();

        var dance = await new DanceService(db, NullLogger<DanceService>.Instance)
            .CreateAsync(new Dal.Entities.Dance { Name = "x" });
        var member = await new MemberService(db, NullLogger<MemberService>.Instance)
            .CreateAsync(TestData.Member(login: "novy"));

        ResultAssert.Failed(dance);
        ResultAssert.Failed(member);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_nad_nepouzitelnym_kontextem_nevyhodi_vyjimku()
    {
        var db = await DisposedContextAsync();

        var result = await new AttendanceService(db, NullLogger<AttendanceService>.Instance)
            .CreateOrUpdateAsync(TestData.RehearsalAttendance(
                memberId: 1,
                new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc),
                Dal.Entities.AttendanceStatus.Yes));

        ResultAssert.Failed(result);
        // Volající se rozhoduje podle Id != 0, takže po neúspěchu tam klíč nesmí být.
        Assert.Equal(0, result.Value);
    }

    /// <summary>
    /// Nenalezení je <c>NotFound</c>, ne <c>Failure</c> — na tom stojí mapování
    /// na HTTP 404 v <c>ResultHttpExtensions</c>. A hlavně to musí být stejné
    /// u všech služeb; dřív <c>EventService</c> místo toho házela výjimku.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_neexistujiciho_zaznamu_vrati_NotFound_u_vsech_sluzeb()
    {
        await using var db = _fixture.NewContext();

        ResultAssert.Failed(
            await new DanceService(db, NullLogger<DanceService>.Instance).DeleteAsync(9999),
            ResultErrorKind.NotFound);
        ResultAssert.Failed(
            await new EventService(db, NullLogger<EventService>.Instance).DeleteAsync(9999),
            ResultErrorKind.NotFound);
        ResultAssert.Failed(
            await new MemberService(db, NullLogger<MemberService>.Instance).DeleteAsync(9999),
            ResultErrorKind.NotFound);
        ResultAssert.Failed(
            await new VideoLinkService(db, NullLogger<VideoLinkService>.Instance).DeleteAsync(9999),
            ResultErrorKind.NotFound);
        ResultAssert.Failed(
            await new AttendanceService(db, NullLogger<AttendanceService>.Instance).DeleteAsync(9999),
            ResultErrorKind.NotFound);
    }
}
