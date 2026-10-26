using Demizon.Core.Services.Member;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// Pravidla krojárky, která drží samotná databáze (unikátní indexy, CHECK constrainty), a to,
/// že soft-delete člena nesmaže z přehledu jeho kroje.
/// </summary>
public class CostumeModelTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "spravce");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private static CostumeItem Item(CostumePart part, string code, int quantity = 1) => new()
    {
        Part = part, Code = code, Size = "4", Quantity = quantity
    };

    private async Task<(Member Member, CostumeItem Item)> SeedAsync(DemizonContext db)
    {
        var member = await TestData.SeedMemberAsync(db, "drzitel");
        var part = new CostumePart
        {
            Name = "Kopanická vesta", Gender = CostumeGender.Male, TrackingMode = CostumeTrackingMode.Individual,
            Costume = new Costume { Name = "Kopanický" },
        };
        var item = Item(part, "K0001");
        db.CostumeItems.Add(item);
        await db.SaveChangesAsync();
        return (member, item);
    }

    [Fact]
    public async Task Inventarni_kod_je_unikatni()
    {
        await using var db = _fixture.NewContext();
        var (_, item) = await SeedAsync(db);

        db.CostumeItems.Add(Item(item.Part, "K0001"));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Clen_drzi_polozku_jen_jednim_radkem()
    {
        await using var db = _fixture.NewContext();
        var (member, item) = await SeedAsync(db);
        db.CostumeHoldings.Add(new CostumeHolding { ItemId = item.Id, MemberId = member.Id, Quantity = 1 });
        await db.SaveChangesAsync();

        db.CostumeHoldings.Add(new CostumeHolding { ItemId = item.Id, MemberId = member.Id, Quantity = 1 });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Drzeni_s_nekladnym_poctem_databaze_odmitne(int quantity)
    {
        await using var db = _fixture.NewContext();
        var (member, item) = await SeedAsync(db);

        db.CostumeHoldings.Add(new CostumeHolding { ItemId = item.Id, MemberId = member.Id, Quantity = quantity });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Predavka_z_krojarky_do_krojarky_neprojde()
    {
        await using var db = _fixture.NewContext();
        var (member, _) = await SeedAsync(db);

        db.CostumeTransfers.Add(new CostumeTransfer { CreatedById = member.Id });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Predavka_clenovi_od_nej_sameho_neprojde()
    {
        await using var db = _fixture.NewContext();
        var (member, _) = await SeedAsync(db);

        db.CostumeTransfers.Add(new CostumeTransfer
        {
            FromMemberId = member.Id, ToMemberId = member.Id, CreatedById = member.Id
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Fotka_bez_predavky_i_bez_soucastky_neprojde()
    {
        await using var db = _fixture.NewContext();
        var (member, _) = await SeedAsync(db);

        db.CostumePhotos.Add(new CostumePhoto
        {
            Data = [1], ThumbnailData = [1], FileSize = 1, UploadedById = member.Id
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task QR_token_clena_je_unikatni()
    {
        await using var db = _fixture.NewContext();
        var first = await TestData.SeedMemberAsync(db, "prvni");
        var second = await TestData.SeedMemberAsync(db, "druhy");
        first.CostumeQrToken = "stejny";
        second.CostumeQrToken = "stejny";

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>
    /// Proč entity krojárky nemají navigaci na <c>Member</c>: přes povinnou navigaci by soft-delete
    /// filtr řádky smazaného člena zahodil (viz <c>SoftDeleteRelationTests</c>). Bývalý člen, který
    /// pořád drží kroj, je přitom přesně to, co má správce vidět.
    /// </summary>
    [Fact]
    public async Task Drzeni_smazaneho_clena_zustava_videt()
    {
        await using (var db = _fixture.NewContext())
        {
            var (member, item) = await SeedAsync(db);
            db.CostumeHoldings.Add(new CostumeHolding { ItemId = item.Id, MemberId = member.Id, Quantity = 1 });
            await db.SaveChangesAsync();
            ResultAssert.Ok(await new MemberService(db, NullLogger<MemberService>.Instance).DeleteAsync(member.Id));
        }

        await using var verify = _fixture.NewContext();
        var holding = await verify.CostumeHoldings.Include(h => h.Item).SingleAsync();
        Assert.Equal("K0001", holding.Item.Code);
    }
}
