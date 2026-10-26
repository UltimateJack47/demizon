using Demizon.Common;
using Demizon.Core.Services.Costume;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

public class CostumeMaintenanceAndInventoryTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "spravce");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private static CostumeMaintenanceService Maintenance(DemizonContext db) =>
        new(db, NullLogger<CostumeMaintenanceService>.Instance);

    private static CostumeInventoryService Inventory(DemizonContext db) =>
        new(db, CostumeTestData.CatalogService(db), NullLogger<CostumeInventoryService>.Instance);

    // ------------------------------------------------------------ údržba

    [Fact]
    public async Task Udrzba_zamkne_kus_pro_predavky_a_ukonceni_ho_vrati()
    {
        await using var db = _fixture.NewContext();
        var keeper = await TestData.SeedMemberAsync(db, "spravce");
        var jana = await TestData.SeedMemberAsync(db, "jana");
        var item = await CostumeTestData.SeedItemAsync(db, "K0001");

        var id = ResultAssert.Ok(await Maintenance(db).StartAsync(item.Id, CostumeMaintenanceKind.Washing, "Skvrna", null, keeper.Id));
        var blocked = await CostumeTestData.TransferService(db)
            .CreateDraftAsync(new CostumeActor(keeper.Id, true), null, jana.Id, [new("K0001")], null);
        ResultAssert.Failed(blocked, ResultErrorKind.Rejected);
        Assert.Contains("údržbě", blocked.Error);

        ResultAssert.Ok(await Maintenance(db).FinishAsync(id, null, 150));

        await using var verify = _fixture.NewContext();
        Assert.Equal(CostumeItemState.Available, (await verify.CostumeItems.SingleAsync()).State);
        var record = await verify.CostumeMaintenances.SingleAsync();
        Assert.Equal("Skvrna", record.Note);
        Assert.Equal(150, record.CostCzk);
        Assert.NotNull(record.FinishedAt);
    }

    [Fact]
    public async Task Pujceny_kus_do_udrzby_nejde()
    {
        await using var db = _fixture.NewContext();
        var jana = await TestData.SeedMemberAsync(db, "jana");
        var item = await CostumeTestData.SeedItemAsync(db, "K0001");
        await CostumeTestData.HoldAsync(db, item.Id, jana.Id);

        ResultAssert.Failed(await Maintenance(db).StartAsync(item.Id, CostumeMaintenanceKind.Repair, null, null, jana.Id),
            ResultErrorKind.Rejected);
    }

    // ---------------------------------------------------------- inventura

    [Fact]
    public async Task Inventura_najde_chybejici_nadbytecny_a_kus_vedeny_u_clena()
    {
        await using var db = _fixture.NewContext();
        var keeper = await TestData.SeedMemberAsync(db, "spravce");
        var jana = await TestData.SeedMemberAsync(db, "jana");
        await CostumeTestData.SeedItemAsync(db, "K0001");                 // v krojárce, naskenovaný
        await CostumeTestData.SeedItemAsync(db, "K0002");                 // v krojárce, chybí
        var held = await CostumeTestData.SeedItemAsync(db, "K0003");      // u Jany, ale leží tu
        await CostumeTestData.HoldAsync(db, held.Id, jana.Id);
        await CostumeTestData.SeedItemAsync(db, "S0001", CostumeTrackingMode.Bulk, quantity: 10);
        var lost = await CostumeTestData.SeedItemAsync(db, "K0004");      // ztracený, našel se
        lost.State = CostumeItemState.Lost;
        await db.SaveChangesAsync();

        var inventory = Inventory(db);
        var checkId = ResultAssert.Ok(await inventory.StartAsync(keeper.Id));
        ResultAssert.Ok(await inventory.ScanAsync(checkId, "k0001"));
        ResultAssert.Ok(await inventory.ScanAsync(checkId, "K0001"));
        ResultAssert.Ok(await inventory.ScanAsync(checkId, "K0003"));
        ResultAssert.Ok(await inventory.ScanAsync(checkId, "S0001", setCount: 12));
        ResultAssert.Ok(await inventory.ScanAsync(checkId, "K0004"));

        var result = (await inventory.GetResultAsync(checkId)).ToDictionary(r => r.Item.Code);

        Assert.Equal(CostumeInventoryFinding.Ok, result["K0001"].Finding);
        Assert.Equal(1, result["K0001"].Counted);
        Assert.Equal(CostumeInventoryFinding.Missing, result["K0002"].Finding);
        Assert.Equal(CostumeInventoryFinding.HeldByMember, result["K0003"].Finding);
        Assert.Equal(CostumeInventoryFinding.Surplus, result["S0001"].Finding);
        Assert.Equal(10, result["S0001"].Expected);
        Assert.Equal(CostumeInventoryFinding.UnexpectedState, result["K0004"].Finding);
    }

    [Fact]
    public async Task Druha_rozpracovana_inventura_nejde_a_do_uzavrene_se_nezapisuje()
    {
        await using var db = _fixture.NewContext();
        var keeper = await TestData.SeedMemberAsync(db, "spravce");
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var inventory = Inventory(db);
        var checkId = ResultAssert.Ok(await inventory.StartAsync(keeper.Id));

        ResultAssert.Failed(await inventory.StartAsync(keeper.Id), ResultErrorKind.Rejected);
        ResultAssert.Ok(await inventory.FinishAsync(checkId));
        ResultAssert.Failed(await inventory.ScanAsync(checkId, "K0001"), ResultErrorKind.Rejected);
        ResultAssert.Ok(await inventory.StartAsync(keeper.Id));
    }

    [Fact]
    public async Task Kus_v_udrzbe_inventura_nekontroluje()
    {
        await using var db = _fixture.NewContext();
        var keeper = await TestData.SeedMemberAsync(db, "spravce");
        var item = await CostumeTestData.SeedItemAsync(db, "K0001");
        ResultAssert.Ok(await Maintenance(db).StartAsync(item.Id, CostumeMaintenanceKind.Cleaning, null, null, keeper.Id));
        var inventory = Inventory(db);
        var checkId = ResultAssert.Ok(await inventory.StartAsync(keeper.Id));

        Assert.Empty(await inventory.GetResultAsync(checkId));
    }
}
