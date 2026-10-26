using Demizon.Core.Services.Costume;
using Demizon.Core.Services.Member;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

public class CostumeQrServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "spravce");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private CostumeQrService Service(Demizon.Dal.DemizonContext db) => new(db, NullLogger<CostumeQrService>.Instance);

    [Fact]
    public async Task Token_vznikne_jednou_a_pretrva_dokud_se_nepregeneruje()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var service = Service(db);

        var first = ResultAssert.Ok(await service.GetOrCreateTokenAsync(member.Id));
        var again = ResultAssert.Ok(await service.GetOrCreateTokenAsync(member.Id));
        var regenerated = ResultAssert.Ok(await service.RegenerateTokenAsync(member.Id));

        Assert.Equal(first, again);
        Assert.NotEqual(first, regenerated);
        Assert.Null(await service.FindMemberAsync(first));
        Assert.Equal(member.Id, (await service.FindMemberAsync(regenerated))!.Id);
    }

    [Fact]
    public async Task Externistu_ani_smazaneho_clena_token_nenajde()
    {
        await using var db = _fixture.NewContext();
        var external = TestData.Member(login: "externi", isExternal: true);
        db.Members.Add(external);
        var deleted = await TestData.SeedMemberAsync(db, "byvaly");
        await db.SaveChangesAsync();
        var service = Service(db);
        var externalToken = ResultAssert.Ok(await service.GetOrCreateTokenAsync(external.Id));
        var deletedToken = ResultAssert.Ok(await service.GetOrCreateTokenAsync(deleted.Id));
        ResultAssert.Ok(await new MemberService(db, NullLogger<MemberService>.Instance).DeleteAsync(deleted.Id));

        Assert.Null(await service.FindMemberAsync(externalToken));
        Assert.Null(await service.FindMemberAsync(deletedToken));
    }
}
