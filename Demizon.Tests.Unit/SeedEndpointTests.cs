using System.Net;
using System.Net.Http.Json;
using Demizon.Contracts.Auth;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit;

/// <summary>
/// <c>POST /api/database/seed</c> zakládá prvního admina, takže je to jediná
/// cesta, jak z venku vyrobit účet s plnými právy. Testy tu hlídají všechny tři
/// pojistky: vypnuto bez tokenu, 401 se špatným tokenem, 409 nad neprázdnou
/// databází. Dřív byl endpoint <c>[AllowAnonymous]</c> bez tokenu a zakládal
/// admina se zadrátovaným heslem, které navíc vracel v těle odpovědi.
/// </summary>
/// <remarks>
/// Testy, které nic nezakládají, jedou nad sdílenou fixture. Ten jediný, který
/// admina opravdu vytvoří, si bere <b>vlastní</b> host — jinak by závisel na
/// pořadí, které xUnit v rámci třídy negarantuje.
/// </remarks>
[Collection("WebHost")]
public class SeedEndpointTests : IAsyncLifetime
{
    private readonly SeedApiFactory _seedFactory;
    private readonly AuthApiFactory _authFactory;
    private HttpClient _seedClient = null!;

    public SeedEndpointTests(SeedApiFactory seedFactory, AuthApiFactory authFactory)
    {
        _seedFactory = seedFactory;
        _authFactory = authFactory;
    }

    public Task InitializeAsync()
    {
        _seedClient = _seedFactory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _seedClient.Dispose();
        return Task.CompletedTask;
    }

    private const string ValidPassword = "dost-dlouhe-heslo-1";

    private static SeedAdminRequest ValidRequest(string login = "prvni-admin") =>
        new(login, ValidPassword, "První", "Admin", "admin@demizon.test");

    private static HttpRequestMessage Post(SeedAdminRequest body, string? token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/database/seed")
        {
            Content = JsonContent.Create(body)
        };
        if (token is not null)
            message.Headers.Add("X-Seed-Token", token);
        return message;
    }

    // ------------------------------------------------------- vypnutý endpoint

    [Fact]
    public async Task Bez_nakonfigurovaneho_tokenu_endpoint_neexistuje()
    {
        // AuthApiFactory schválně SeedToken nenastavuje — to je výchozí stav
        // běžící instance, kde bootstrap už proběhl.
        using var client = _authFactory.CreateClient();

        var response = await client.SendAsync(Post(ValidRequest(), token: null));

        await AssertStatusAsync(HttpStatusCode.NotFound, response);
    }

    [Fact]
    public async Task Bez_nakonfigurovaneho_tokenu_neprojde_ani_token_jineho_hostu()
    {
        using var client = _authFactory.CreateClient();

        var response = await client.SendAsync(Post(ValidRequest(), SeedApiFactory.SeedToken));

        // 404, ne 401 — z odpovědi se nesmí poznat, že tady endpoint vůbec je.
        await AssertStatusAsync(HttpStatusCode.NotFound, response);
    }

    // ------------------------------------------------------------------ token

    [Fact]
    public async Task Bez_hlavicky_s_tokenem_vrati_401()
    {
        var response = await _seedClient.SendAsync(Post(ValidRequest("bez-hlavicky"), token: null));

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        Assert.False(await LoginExistsAsync("bez-hlavicky"));
    }

    [Fact]
    public async Task Spatny_token_vrati_401_a_nic_nezaklada()
    {
        var response = await _seedClient.SendAsync(
            Post(ValidRequest("spatny-token"), "uplne-jiny-token"));

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        Assert.False(await LoginExistsAsync("spatny-token"));
    }

    [Fact]
    public async Task Token_se_spravnym_prefixem_ale_kratsi_vrati_401()
    {
        // Regrese na porovnání v konstantním čase: rozdílná délka musí skončit
        // false, ne výjimkou z FixedTimeEquals.
        var prefix = SeedApiFactory.SeedToken[..^3];

        var response = await _seedClient.SendAsync(Post(ValidRequest("prefix"), prefix));

        await AssertStatusAsync(HttpStatusCode.Unauthorized, response);
        Assert.False(await LoginExistsAsync("prefix"));
    }

    // -------------------------------------------------------- validace vstupu

    [Theory]
    [InlineData("ab", ValidPassword)]         // login < 3 znaky
    [InlineData("validni-login", "kratke")]   // heslo < 12 znaků
    [InlineData("validni-login", "")]         // prázdné heslo
    public async Task Nevalidni_vstup_vrati_400(string login, string password)
    {
        var body = new SeedAdminRequest(login, password, "První", "Admin");

        var response = await _seedClient.SendAsync(Post(body, SeedApiFactory.SeedToken));

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        Assert.False(await LoginExistsAsync(login));
    }

    // ------------------------------------------------- úspěch a jednorázovost

    [Fact]
    public async Task Se_spravnym_tokenem_zaklada_admina_a_druhy_pokus_vrati_409()
    {
        const string login = "bootstrap-admin";

        // Vlastní host s vlastní prázdnou databází — tenhle test do ní zapisuje.
        using var factory = new SeedApiFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(
            Post(new SeedAdminRequest(login, ValidPassword, "První", "Admin", "a@demizon.test"),
                SeedApiFactory.SeedToken));

        await AssertStatusAsync(HttpStatusCode.OK, response);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ValidPassword, payload);
        Assert.Contains(login, payload);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
            var admin = await db.Members.SingleAsync(m => m.Login == login);
            Assert.Equal(UserRole.Admin, admin.Role);
            Assert.NotEqual(ValidPassword, admin.PasswordHash);
            Assert.Null(admin.DeletedAt);
        }

        // Jednorázovost: druhý pokus už narazí na neprázdnou databázi.
        var second = await client.SendAsync(
            Post(ValidRequest("druhy-admin"), SeedApiFactory.SeedToken));
        await AssertStatusAsync(HttpStatusCode.Conflict, second);
    }

    [Fact]
    public async Task Soft_smazany_clen_taky_blokuje_seed()
    {
        // Soft delete nechává login i hash v tabulce, takže „prázdná databáze“
        // musí znamenat i žádný smazaný člen — jinak by šlo přes seed obejít
        // deaktivovaný účet.
        using var factory = new SeedApiFactory();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
            db.Members.Add(new Member
            {
                Name = "Smazany",
                Surname = "Clen",
                Login = "smazany",
                PasswordHash = "hash",
                Role = UserRole.Standard,
                DeletedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.SendAsync(Post(ValidRequest(), SeedApiFactory.SeedToken));

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
    }

    /// <summary>
    /// Při neshodě přiloží tělo odpovědi. Bez toho je z „expected 401, actual 500“
    /// jen hádání, co se v hostu pokazilo.
    /// </summary>
    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected) return;
        var body = await response.Content.ReadAsStringAsync();
        Assert.Fail(
            $"Ocekavano {expected}, prislo {(int)response.StatusCode} {response.StatusCode}."
            + Environment.NewLine + body);
    }

    private async Task<bool> LoginExistsAsync(string login)
    {
        using var scope = _seedFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        return await db.Members.IgnoreQueryFilters().AnyAsync(m => m.Login == login);
    }
}
