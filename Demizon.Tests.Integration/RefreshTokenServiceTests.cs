using CryptoHelper;
using Demizon.Core.Services.Authentication;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.Integration;

/// <summary>
/// Refresh tokeny jsou bezpečnostně nejcitlivější část backendu, takže jim patří
/// nejpodrobnější sada testů: raw hodnota nesmí nikdy skončit v DB, token musí být
/// jednorázový a rotace nesmí být zneužitelná k replay útoku.
/// </summary>
public class RefreshTokenServiceTests : IAsyncDisposable
{
    private const int ExpirationDays = 30;

    private readonly DatabaseFixture _fixture = new(currentUserLogin: "tester");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private async Task<int> SeedMemberIdAsync()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        return member.Id;
    }

    // ---------------------------------------------------------------- vytvoření

    [Fact]
    public async Task CreateAsync_neuklada_raw_token_ale_jeho_hash()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();

        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        var stored = await verify.RefreshTokens.SingleAsync();
        Assert.NotEqual(rawToken, stored.TokenHash);
        Assert.DoesNotContain(rawToken, stored.TokenHash);
        Assert.True(PasswordHasher.VerifyHashedPassword(stored.TokenHash, rawToken));
    }

    [Fact]
    public async Task CreateAsync_ulozi_prvnich_osm_znaku_jako_prefix_pro_index()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();

        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        var stored = await verify.RefreshTokens.SingleAsync();
        Assert.Equal(rawToken[..8], stored.TokenPrefix);
    }

    [Fact]
    public async Task CreateAsync_nastavi_expiraci_podle_parametru()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();

        await new RefreshTokenService(db).CreateAsync(memberId, expirationDays: 7);

        await using var verify = _fixture.NewContext();
        var stored = await verify.RefreshTokens.SingleAsync();
        Assert.InRange(stored.ExpiresAt,
            DateTime.UtcNow.AddDays(7).AddMinutes(-1),
            DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    [Fact]
    public async Task CreateAsync_generuje_pokazde_jinou_hodnotu()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var service = new RefreshTokenService(db);

        var first = await service.CreateAsync(memberId, ExpirationDays);
        var second = await service.CreateAsync(memberId, ExpirationDays);

        Assert.NotEqual(first, second);
    }

    /// <summary>Nový token odvolá předchozí platné — jeden člen má aktivní jen jeden token.</summary>
    [Fact]
    public async Task CreateAsync_odvola_predchozi_platne_tokeny_stejneho_clena()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var service = new RefreshTokenService(db);

        var oldToken = await service.CreateAsync(memberId, ExpirationDays);
        await service.CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        Assert.Null(await new RefreshTokenService(verify).ValidateAsync(oldToken));
        Assert.Equal(1, await verify.RefreshTokens.CountAsync(t => !t.IsRevoked));
    }

    [Fact]
    public async Task CreateAsync_neodvola_tokeny_jinych_clenu()
    {
        await using var seed = _fixture.NewContext();
        var first = await TestData.SeedMemberAsync(seed, "prvni");
        var second = await TestData.SeedMemberAsync(seed, "druhy");

        await using var db = _fixture.NewContext();
        var service = new RefreshTokenService(db);
        var firstToken = await service.CreateAsync(first.Id, ExpirationDays);
        await service.CreateAsync(second.Id, ExpirationDays);

        await using var verify = _fixture.NewContext();
        Assert.Equal(first.Id, await new RefreshTokenService(verify).ValidateAsync(firstToken));
    }

    // ---------------------------------------------------------------- validace

    [Fact]
    public async Task ValidateAsync_platneho_tokenu_vrati_id_clena()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        Assert.Equal(memberId, await new RefreshTokenService(verify).ValidateAsync(rawToken));
    }

    /// <summary>
    /// Token je jednorázový: <c>ValidateAsync</c> ho podmíněným UPDATE hned odvolá.
    /// Druhé použití stejné hodnoty je právě ten replay, který má rotace zachytit.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_je_jednorazova_druhe_pouziti_vrati_null()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        var service = new RefreshTokenService(verify);

        Assert.Equal(memberId, await service.ValidateAsync(rawToken));
        Assert.Null(await service.ValidateAsync(rawToken));
    }

    [Fact]
    public async Task ValidateAsync_po_uspesnem_pouziti_oznaci_token_jako_odvolany()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var use = _fixture.NewContext();
        await new RefreshTokenService(use).ValidateAsync(rawToken);

        await using var verify = _fixture.NewContext();
        Assert.True(await verify.RefreshTokens.AllAsync(t => t.IsRevoked));
    }

    [Fact]
    public async Task ValidateAsync_expirovaneho_tokenu_vrati_null()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var expire = _fixture.NewContext();
        var stored = await expire.RefreshTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await expire.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        Assert.Null(await new RefreshTokenService(verify).ValidateAsync(rawToken));
    }

    [Fact]
    public async Task ValidateAsync_rucne_odvolaneho_tokenu_vrati_null()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var revoke = _fixture.NewContext();
        var stored = await revoke.RefreshTokens.SingleAsync();
        stored.IsRevoked = true;
        await revoke.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        Assert.Null(await new RefreshTokenService(verify).ValidateAsync(rawToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public async Task ValidateAsync_prilis_kratky_nebo_prazdny_vstup_vrati_null(string? rawToken)
    {
        await using var db = _fixture.NewContext();

        Assert.Null(await new RefreshTokenService(db).ValidateAsync(rawToken));
    }

    [Fact]
    public async Task ValidateAsync_neznameho_tokenu_vrati_null()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        Assert.Null(await new RefreshTokenService(verify)
            .ValidateAsync("uplne-jina-hodnota-ktera-v-databazi-neni"));
    }

    /// <summary>
    /// Prefix je jen index pro předfiltrování — samotná shoda prefixu bez shody hashe
    /// nesmí token uznat.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_shoda_prefixu_bez_shody_hashe_vrati_null()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        var rawToken = await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        var forged = rawToken[..8] + "-podvrzeny-zbytek-tokenu";

        await using var verify = _fixture.NewContext();
        Assert.Null(await new RefreshTokenService(verify).ValidateAsync(forged));
    }

    [Fact]
    public async Task ValidateAsync_rozlisi_dva_tokeny_se_shodnym_prefixem()
    {
        await using var seed = _fixture.NewContext();
        var first = await TestData.SeedMemberAsync(seed, "prvni");
        var second = await TestData.SeedMemberAsync(seed, "druhy");

        // Prefix se odvozuje z raw hodnoty, takže kolizi je nutné vyrobit tak, že se dva
        // tokeny shodují v prvních osmi znacích. Náhodně generovaná hodnota to neumožní,
        // proto se řádky zapisují ručně — testuje se právě to, že o shodě rozhodne až hash.
        const string sharedPrefix = "AAAAAAAA";
        var firstRaw = sharedPrefix + "-token-prvniho-clena";
        var secondRaw = sharedPrefix + "-token-druheho-clena";

        await using var db = _fixture.NewContext();
        db.RefreshTokens.AddRange(
            NewToken(first.Id, firstRaw, sharedPrefix),
            NewToken(second.Id, secondRaw, sharedPrefix));
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var service = new RefreshTokenService(verify);
        Assert.Equal(second.Id, await service.ValidateAsync(secondRaw));
        Assert.Equal(first.Id, await service.ValidateAsync(firstRaw));
    }

    private static RefreshToken NewToken(int memberId, string rawToken, string prefix) => new()
    {
        MemberId = memberId,
        TokenHash = PasswordHasher.HashPassword(rawToken),
        TokenPrefix = prefix,
        ExpiresAt = DateTime.UtcNow.AddDays(ExpirationDays),
        CreatedAt = DateTime.UtcNow
    };

    // ---------------------------------------------------------------- kaskáda

    [Fact]
    public async Task Smazani_clena_kaskadove_smaze_jeho_refresh_tokeny()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var delete = _fixture.NewContext();
        // Tvrdé smazání (ne soft delete) — testuje se konfigurace OnDelete(Cascade).
        var member = await delete.Members.SingleAsync(m => m.Id == memberId);
        delete.Members.Remove(member);
        await delete.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.RefreshTokens.Where(t => t.MemberId == memberId).ToListAsync());
    }

    [Fact]
    public async Task Refresh_token_je_vazany_na_konkretniho_clena()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        await new RefreshTokenService(db).CreateAsync(member.Id, ExpirationDays);

        await using var verify = _fixture.NewContext();
        var stored = await verify.RefreshTokens.Include(t => t.Member).SingleAsync();
        Assert.Equal(member.Id, stored.MemberId);
        Assert.Equal("tester", stored.Member.Login);
    }

    [Fact]
    public async Task Hash_tokenu_ma_format_ASP_NET_Identity_V3()
    {
        var memberId = await SeedMemberIdAsync();
        await using var db = _fixture.NewContext();
        await new RefreshTokenService(db).CreateAsync(memberId, ExpirationDays);

        await using var verify = _fixture.NewContext();
        var stored = await verify.RefreshTokens.SingleAsync();

        // Marker 0x01 na začátku = formát V3. CryptoHelper 5.x při ověřování odmítá
        // hashe s parametry pod svými limity, takže formát je součást kontraktu.
        var decoded = Convert.FromBase64String(stored.TokenHash);
        Assert.Equal(0x01, decoded[0]);
    }
}
