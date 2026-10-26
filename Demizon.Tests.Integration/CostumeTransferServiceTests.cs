using Demizon.Common;
using Demizon.Common.Configuration;
using Demizon.Core.Services.Costume;
using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// Stavový automat předávek a to, co z něj plyne pro držení: kdo smí co založit, kdo potvrzuje,
/// že kus nejde předat dvakrát a že se držení mění až potvrzením.
/// </summary>
public class CostumeTransferServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "spravce");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private int _keeperId;
    private int _janaId;
    private int _petrId;

    private CostumeActor Keeper => new(_keeperId, IsKeeper: true);
    private CostumeActor Jana => new(_janaId, IsKeeper: false);
    private CostumeActor Petr => new(_petrId, IsKeeper: false);

    private async Task SeedMembersAsync()
    {
        await using var db = _fixture.NewContext();
        _keeperId = (await TestData.SeedMemberAsync(db, "spravce")).Id;
        _janaId = (await TestData.SeedMemberAsync(db, "jana")).Id;
        _petrId = (await TestData.SeedMemberAsync(db, "petr")).Id;
    }

    private CostumeTransferService Service(DemizonContext db, CostumeSettings? settings = null) =>
        CostumeTestData.TransferService(db, settings);

    /// <summary>Založí, nafotí a odešle předávku; vrací její id.</summary>
    private async Task<int> SubmitAsync(CostumeActor actor, int? from, int? to, params CostumeLineInput[] lines)
    {
        await using var db = _fixture.NewContext();
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(actor, from, to, lines, null));
        foreach (var line in (await service.GetAsync(id))!.Lines)
            ResultAssert.Ok(await service.AddPhotoAsync(id, line.Id, actor, CostumeTestData.Jpeg()));
        ResultAssert.Ok(await service.SubmitAsync(id, actor));
        return id;
    }

    private async Task<int> HeldAsync(string code, int memberId)
    {
        await using var db = _fixture.NewContext();
        return await db.CostumeHoldings.Where(h => h.Item.Code == code && h.MemberId == memberId)
            .SumAsync(h => h.Quantity);
    }

    // ------------------------------------------------- výdej z krojárky

    [Fact]
    public async Task Vydej_z_krojarky_meni_drzeni_az_potvrzenim_prijemce()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
            await CostumeTestData.SeedItemAsync(db, "K0001");

        var id = await SubmitAsync(Keeper, null, _janaId, new CostumeLineInput("K0001"));
        Assert.Equal(0, await HeldAsync("K0001", _janaId));

        await using (var db = _fixture.NewContext())
            ResultAssert.Ok(await Service(db).ConfirmAsync(id, Jana));

        Assert.Equal(1, await HeldAsync("K0001", _janaId));
        await using var verify = _fixture.NewContext();
        var transfer = await verify.CostumeTransfers.SingleAsync(t => t.Id == id);
        Assert.Equal(CostumeTransferStatus.Confirmed, transfer.Status);
        Assert.Equal(_janaId, transfer.ResolvedById);
    }

    [Fact]
    public async Task Bez_fotky_u_kazdeho_kusu_nejde_predavku_odeslat()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        await CostumeTestData.SeedItemAsync(db, "K0002");
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId,
            [new CostumeLineInput("K0001"), new CostumeLineInput("K0002")], null));
        var first = (await service.GetAsync(id))!.Lines.Single(l => l.Item.Code == "K0001");
        ResultAssert.Ok(await service.AddPhotoAsync(id, first.Id, Keeper, CostumeTestData.Jpeg()));

        var submitted = await service.SubmitAsync(id, Keeper);

        ResultAssert.Failed(submitted, ResultErrorKind.Rejected);
        Assert.Contains("K0002", submitted.Error);
        Assert.DoesNotContain("K0001", submitted.Error);
    }

    [Fact]
    public async Task Potvrdit_muze_jen_prijemce_ne_spravce_ani_nikdo_jiny()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
            await CostumeTestData.SeedItemAsync(db, "K0001");
        var id = await SubmitAsync(Keeper, null, _janaId, new CostumeLineInput("K0001"));

        await using var confirmDb = _fixture.NewContext();
        ResultAssert.Failed(await Service(confirmDb).ConfirmAsync(id, Keeper), ResultErrorKind.Rejected);
        ResultAssert.Failed(await Service(confirmDb).ConfirmAsync(id, Petr), ResultErrorKind.Rejected);
        Assert.Equal(0, await HeldAsync("K0001", _janaId));
    }

    [Fact]
    public async Task Kus_v_otevrene_predavce_nejde_dat_do_dalsi_a_po_zruseni_zase_ano()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var service = Service(db);
        var first = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("K0001")], null));

        var second = await service.CreateDraftAsync(Keeper, null, _petrId, [new("K0001")], null);
        ResultAssert.Failed(second, ResultErrorKind.Rejected);
        Assert.Contains("jiné předávce", second.Error);

        ResultAssert.Ok(await service.CancelAsync(first, Keeper));
        ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _petrId, [new("K0001")], null));
    }

    // ------------------------------------------- předání mezi členy

    [Fact]
    public async Task Clen_preda_kus_kolegovi_bez_spravce()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
        {
            var item = await CostumeTestData.SeedItemAsync(db, "K0001");
            await CostumeTestData.HoldAsync(db, item.Id, _janaId);
        }

        var id = await SubmitAsync(Jana, _janaId, _petrId, new CostumeLineInput("K0001"));
        await using (var db = _fixture.NewContext())
            ResultAssert.Ok(await Service(db).ConfirmAsync(id, Petr));

        Assert.Equal(0, await HeldAsync("K0001", _janaId));
        Assert.Equal(1, await HeldAsync("K0001", _petrId));
    }

    [Fact]
    public async Task Clen_nepreda_kus_ktery_nema_ani_nevyda_z_krojarky_ani_nezalozi_vraceni()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        var item = await CostumeTestData.SeedItemAsync(db, "K0001");
        await CostumeTestData.HoldAsync(db, item.Id, _petrId);
        var service = Service(db);

        ResultAssert.Failed(await service.CreateDraftAsync(Jana, _janaId, _petrId, [new("K0001")], null),
            ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.CreateDraftAsync(Jana, null, _janaId, [new("K0001")], null),
            ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.CreateDraftAsync(Jana, _petrId, _janaId, [new("K0001")], null),
            ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.CreateDraftAsync(Petr, _petrId, null, [new("K0001")], null),
            ResultErrorKind.Rejected);
    }

    [Fact]
    public async Task Prijemce_externista_nejde_protoze_by_prevzeti_nepotvrdil()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var external = TestData.Member(login: "externi", isExternal: true);
        db.Members.Add(external);
        await db.SaveChangesAsync();

        ResultAssert.Failed(await Service(db).CreateDraftAsync(Keeper, null, external.Id, [new("K0001")], null),
            ResultErrorKind.Rejected);
    }

    // ------------------------------------------------- vrácení

    [Fact]
    public async Task Vraceni_do_krojarky_potvrdi_spravce_uz_odeslanim()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
        {
            var item = await CostumeTestData.SeedItemAsync(db, "K0001");
            await CostumeTestData.HoldAsync(db, item.Id, _janaId);
        }

        var id = await SubmitAsync(Keeper, _janaId, null, new CostumeLineInput("K0001"));

        Assert.Equal(0, await HeldAsync("K0001", _janaId));
        await using var verify = _fixture.NewContext();
        Assert.Equal(CostumeTransferStatus.Confirmed, (await verify.CostumeTransfers.SingleAsync(t => t.Id == id)).Status);
    }

    [Fact]
    public async Task Spravce_vezme_kroj_zpet_i_od_smazaneho_clena()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
        {
            var item = await CostumeTestData.SeedItemAsync(db, "K0001");
            await CostumeTestData.HoldAsync(db, item.Id, _janaId);
            ResultAssert.Ok(await new MemberService(db, NullLogger<MemberService>.Instance).DeleteAsync(_janaId));
        }

        await SubmitAsync(Keeper, _janaId, null, new CostumeLineInput("K0001"));

        Assert.Equal(0, await HeldAsync("K0001", _janaId));
    }

    // ------------------------------------------------- šarže

    [Fact]
    public async Task Sarze_se_deli_vydej_predani_kolegovi_a_castecne_vraceni()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
            await CostumeTestData.SeedItemAsync(db, "S0001", CostumeTrackingMode.Bulk, quantity: 10);

        var issue = await SubmitAsync(Keeper, null, _janaId, new CostumeLineInput("S0001", 3));
        await using (var db = _fixture.NewContext())
            ResultAssert.Ok(await Service(db).ConfirmAsync(issue, Jana));
        var peer = await SubmitAsync(Jana, _janaId, _petrId, new CostumeLineInput("S0001", 1));
        await using (var db = _fixture.NewContext())
            ResultAssert.Ok(await Service(db).ConfirmAsync(peer, Petr));
        await SubmitAsync(Keeper, _janaId, null, new CostumeLineInput("S0001", 2));

        Assert.Equal(0, await HeldAsync("S0001", _janaId));
        Assert.Equal(1, await HeldAsync("S0001", _petrId));
        await using var verify = _fixture.NewContext();
        var itemId = (await verify.CostumeItems.SingleAsync(i => i.Code == "S0001")).Id;
        var stock = (await CostumeTestData.CatalogService(verify).GetStockAsync([itemId]))[itemId];
        Assert.Equal(9, stock.InStore);
    }

    [Fact]
    public async Task Sarze_nepusti_vic_nez_zbyva_po_odecteni_rozpracovanych_predavek()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "S0001", CostumeTrackingMode.Bulk, quantity: 10);
        var service = Service(db);

        ResultAssert.Failed(await service.CreateDraftAsync(Keeper, null, _janaId, [new("S0001", 11)], null),
            ResultErrorKind.Rejected);
        ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("S0001", 8)], null));

        var second = await service.CreateDraftAsync(Keeper, null, _petrId, [new("S0001", 3)], null);
        ResultAssert.Failed(second, ResultErrorKind.Rejected);
        Assert.Contains("jen 2 ks", second.Error);
    }

    // ------------------------------------------------- odmítnutí a změny mezi tím

    [Fact]
    public async Task Odmitnuti_chce_duvod_a_kus_zustane_predavajicimu()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
            await CostumeTestData.SeedItemAsync(db, "K0001");
        var id = await SubmitAsync(Keeper, null, _janaId, new CostumeLineInput("K0001"));

        await using var rejectDb = _fixture.NewContext();
        var service = Service(rejectDb);
        ResultAssert.Failed(await service.RejectAsync(id, Jana, "  "), ResultErrorKind.Rejected);
        ResultAssert.Ok(await service.RejectAsync(id, Jana, "Utržený knoflík, na fotce není vidět."));

        Assert.Equal(0, await HeldAsync("K0001", _janaId));
        // Kus je zase volný pro novou předávku.
        ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _petrId, [new("K0001")], null));
    }

    [Fact]
    public async Task Potvrzeni_znovu_overi_ze_kus_je_k_dispozici()
    {
        await SeedMembersAsync();
        await using (var db = _fixture.NewContext())
            await CostumeTestData.SeedItemAsync(db, "K0001");
        var id = await SubmitAsync(Keeper, null, _janaId, new CostumeLineInput("K0001"));
        await using (var db = _fixture.NewContext())
        {
            (await db.CostumeItems.SingleAsync()).State = CostumeItemState.InMaintenance;
            await db.SaveChangesAsync();
        }

        await using var confirmDb = _fixture.NewContext();
        var confirmed = await Service(confirmDb).ConfirmAsync(id, Jana);

        ResultAssert.Failed(confirmed, ResultErrorKind.Rejected);
        Assert.Contains("údržbě", confirmed.Error);
        Assert.Equal(0, await HeldAsync("K0001", _janaId));
    }

    [Fact]
    public async Task Rozpracovanou_predavku_upravuje_jen_zakladatel()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("K0001")], null));
        var lineId = (await service.GetAsync(id))!.Lines.Single().Id;

        ResultAssert.Failed(await service.AddPhotoAsync(id, lineId, Jana, CostumeTestData.Jpeg()),
            ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.SubmitAsync(id, Jana), ResultErrorKind.Rejected);
    }

    // ------------------------------------------------- fotky

    [Fact]
    public async Task Smazana_fotka_uz_se_do_odeslani_nepocita()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("K0001")], null));
        var lineId = (await service.GetAsync(id))!.Lines.Single().Id;
        var photoId = ResultAssert.Ok(await service.AddPhotoAsync(id, lineId, Keeper, CostumeTestData.Jpeg()));

        ResultAssert.Ok(await service.RemovePhotoAsync(id, photoId, Keeper));

        Assert.Empty(await service.GetPhotoInfosAsync(id));
        ResultAssert.Failed(await service.SubmitAsync(id, Keeper), ResultErrorKind.Rejected);
        ResultAssert.Failed(await service.RemovePhotoAsync(id, photoId, Keeper), ResultErrorKind.NotFound);
    }

    /// <summary>
    /// Web fotku zmenší v prohlížeči a EXIF tím ztratí; čas pořízení pak nese začátek originálu.
    /// </summary>
    [Fact]
    public async Task Cas_porizeni_se_vezme_z_originalu_kdyz_fotku_zmensil_klient()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("K0001")], null));
        var lineId = (await service.GetAsync(id))!.Lines.Single().Id;
        var original = CostumeTestData.Jpeg(new DateTime(2026, 7, 1, 12, 0, 0));
        var head = new MemoryStream(original.ToArray()[..(int)Math.Min(original.Length, 256 * 1024)]);

        ResultAssert.Ok(await service.AddPhotoAsync(id, lineId, Keeper, CostumeTestData.Jpeg(), head));

        // Červenec = letní čas, Praha je UTC+2.
        var photo = Assert.Single(await service.GetPhotoInfosAsync(id));
        Assert.Equal(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), photo.CapturedAt);
        Assert.True(photo.IsStale);
    }

    [Fact]
    public async Task Fotka_ktera_neni_obrazek_se_odmitne()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("K0001")], null));
        var lineId = (await service.GetAsync(id))!.Lines.Single().Id;

        var result = await service.AddPhotoAsync(id, lineId, Keeper, new MemoryStream("not an image"u8.ToArray()));

        ResultAssert.Failed(result, ResultErrorKind.Rejected);
    }

    [Fact]
    public async Task Plne_uloziste_fotek_dalsi_fotku_odmitne()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        var service = Service(db, new CostumeSettings { MaxPhotoStorageBytes = 10 });
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId, [new("K0001")], null));
        var lineId = (await service.GetAsync(id))!.Lines.Single().Id;

        var result = await service.AddPhotoAsync(id, lineId, Keeper, CostumeTestData.Jpeg());

        ResultAssert.Failed(result, ResultErrorKind.Rejected);
        Assert.Contains("plné", result.Error);
    }

    [Fact]
    public async Task Cas_porizeni_z_EXIF_se_ulozi_v_UTC_a_stara_fotka_se_oznaci()
    {
        await SeedMembersAsync();
        await using var db = _fixture.NewContext();
        await CostumeTestData.SeedItemAsync(db, "K0001");
        await CostumeTestData.SeedItemAsync(db, "K0002");
        var service = Service(db);
        var id = ResultAssert.Ok(await service.CreateDraftAsync(Keeper, null, _janaId,
            [new("K0001"), new("K0002")], null));
        var lines = (await service.GetAsync(id))!.Lines.OrderBy(l => l.Item.Code).ToList();

        // Leden = zimní čas, Praha je UTC+1. Fotka je měsíce stará.
        ResultAssert.Ok(await service.AddPhotoAsync(id, lines[0].Id, Keeper,
            CostumeTestData.Jpeg(new DateTime(2026, 1, 10, 14, 0, 0))));
        // Explicitní offset z EXIF má přednost před zónou serveru.
        var nowWithOffset = DateTime.UtcNow.AddHours(2);
        ResultAssert.Ok(await service.AddPhotoAsync(id, lines[1].Id, Keeper,
            CostumeTestData.Jpeg(nowWithOffset, "+02:00")));

        var photos = await service.GetPhotoInfosAsync(id);
        var old = photos.Single(p => p.TransferLineId == lines[0].Id);
        var fresh = photos.Single(p => p.TransferLineId == lines[1].Id);
        Assert.Equal(new DateTime(2026, 1, 10, 13, 0, 0, DateTimeKind.Utc), old.CapturedAt);
        Assert.True(old.IsStale);
        Assert.False(fresh.IsStale);
        Assert.Equal(DateTimeKind.Utc, fresh.CapturedAt!.Value.Kind);
    }
}
