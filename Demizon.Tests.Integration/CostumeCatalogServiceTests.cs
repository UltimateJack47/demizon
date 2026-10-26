using Demizon.Common;
using Demizon.Core.Services.Costume;
using Demizon.Core.Services.Member;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

public class CostumeCatalogServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "spravce");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private async Task<int> PartAsync(CostumeTrackingMode mode = CostumeTrackingMode.Individual)
    {
        await using var db = _fixture.NewContext();
        var costumeId = ResultAssert.Ok(await CostumeTestData.CatalogService(db)
            .CreateCostumeAsync(new CostumeInput("Kopanický", null, 1)));
        return ResultAssert.Ok(await CostumeTestData.CatalogService(db)
            .CreatePartAsync(new CostumePartInput(costumeId, "Vesta", CostumeGender.Male, null, mode)));
    }

    [Fact]
    public async Task Prazdny_kod_se_vygeneruje_v_rade_K()
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);

        var first = ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, null, "4", null, null, null)));
        ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, "k0041", "5", null, null, null)));
        var third = ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, " ", "6", null, null, null)));

        Assert.Equal("K0001", (await service.GetItemAsync(first))!.Code);
        Assert.Equal("K0042", (await service.GetItemAsync(third))!.Code);
        Assert.NotNull(await service.GetItemByCodeAsync(" k0041 "));
    }

    [Theory]
    [InlineData("K 01")]
    [InlineData("Kč01")]
    [InlineData("K/01")]
    public async Task Kod_s_neplatnymi_znaky_se_odmitne(string code)
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();

        ResultAssert.Failed(await CostumeTestData.CatalogService(db)
            .CreateItemAsync(new CostumeItemInput(partId, code, null, null, null, null)), ResultErrorKind.Rejected);
    }

    [Fact]
    public async Task Duplicitni_kod_se_odmitne_srozumitelne()
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);
        ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, "K0001", null, null, null, null)));

        var duplicate = await service.CreateItemAsync(new CostumeItemInput(partId, "k0001", null, null, null, null));

        ResultAssert.Failed(duplicate, ResultErrorKind.Rejected);
        Assert.Contains("K0001", duplicate.Error);
    }

    [Fact]
    public async Task Kus_ma_vzdy_pocet_1_i_kdyz_prijde_vic()
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);

        var id = ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, null, null, null, null, null, 5)));

        Assert.Equal(1, (await service.GetItemAsync(id))!.Quantity);
    }

    [Fact]
    public async Task Pocet_sarze_nejde_snizit_pod_rozpujcene()
    {
        var partId = await PartAsync(CostumeTrackingMode.Bulk);
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);
        var id = ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, null, null, null, null, null, 10)));
        var member = await TestData.SeedMemberAsync(db);
        await CostumeTestData.HoldAsync(db, id, member.Id, 4);

        ResultAssert.Failed(await service.UpdateItemAsync(id, new CostumeItemInput(partId, null, null, null, null, null, 3)),
            ResultErrorKind.Rejected);
        ResultAssert.Ok(await service.UpdateItemAsync(id, new CostumeItemInput(partId, null, null, null, null, null, 4)));
    }

    [Fact]
    public async Task Soucastku_s_kusy_ani_kroj_se_soucastkami_nejde_smazat()
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);
        ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, null, null, null, null, null)));
        var costumeId = (await db.CostumeParts.SingleAsync()).CostumeId!.Value;

        ResultAssert.Failed(await service.DeletePartAsync(partId), ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.DeleteCostumeAsync(costumeId), ResultErrorKind.Rejected);
    }

    [Fact]
    public async Task Rezim_evidence_jde_zmenit_jen_bez_kusu()
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);
        var input = new CostumePartInput(null, "Vesta", CostumeGender.Male, null, CostumeTrackingMode.Bulk);

        ResultAssert.Ok(await service.UpdatePartAsync(partId, input));
        ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, null, null, null, null, null, 3)));

        ResultAssert.Failed(await service.UpdatePartAsync(partId, input with { TrackingMode = CostumeTrackingMode.Individual }),
            ResultErrorKind.Rejected);
    }

    [Fact]
    public async Task Vyradit_nejde_kus_ktery_ma_clen_a_udrzba_se_rucne_nenastavuje()
    {
        var partId = await PartAsync();
        await using var db = _fixture.NewContext();
        var service = CostumeTestData.CatalogService(db);
        var id = ResultAssert.Ok(await service.CreateItemAsync(new CostumeItemInput(partId, null, null, null, null, null)));
        var member = await TestData.SeedMemberAsync(db);
        await CostumeTestData.HoldAsync(db, id, member.Id);

        ResultAssert.Failed(await service.SetItemStateAsync(id, CostumeItemState.Retired), ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.SetItemStateAsync(id, CostumeItemState.InMaintenance), ResultErrorKind.Rejected);
        // Ztracený být může i u člena — právě tam se ztrácí.
        ResultAssert.Ok(await service.SetItemStateAsync(id, CostumeItemState.Lost));
    }

    [Fact]
    public async Task Jmena_drzitelu_zahrnuji_i_smazane_cleny()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        ResultAssert.Ok(await new MemberService(db, NullLogger<MemberService>.Instance).DeleteAsync(member.Id));

        var names = await CostumeTestData.CatalogService(db).GetMemberNamesAsync([member.Id]);

        Assert.True(names[member.Id].IsDeleted);
        Assert.Equal("Jan Novak", names[member.Id].FullName);
    }
}
