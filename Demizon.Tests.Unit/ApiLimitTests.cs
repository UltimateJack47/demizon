using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Contracts.Auth;
using Demizon.Tests.Unit.Fakes;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit;

/// <summary>
/// Limity, které se čtou při registraci služeb, takže je nejde přenastavit
/// u běžícího hosta: kvóta na úložiště a rate limiter na přihlašování.
/// Oba byly v <c>testing-plan.md</c> vedené jako nepokryté.
/// </summary>
[Collection("WebHost")]
public class ApiLimitTests
{
    /// <summary>
    /// Kontrakt uploadového endpointu při přeplněném úložišti: 400 s důvodem
    /// a v databázi nic.
    /// </summary>
    /// <remarks>
    /// Ověřeno spuštěním proti kódu bez opravy z Result refactoringu: test projde
    /// i tak, a je dobré vědět proč. Kvóta se kontroluje **dvakrát** — jednou ve
    /// <c>FileUploadService.UploadImageToDbAsync</c> a pak ještě ve
    /// <c>FileService.CreateAsync</c>. Přes HTTP se vždy uplatní ta první, protože
    /// obě sčítají totéž, takže druhá je obrana do hloubky, na kterou se odsud
    /// nedá dostat. Kontrolu návratové hodnoty ve <c>FilesController</c> proto
    /// hlídá až
    /// <c>StorageQuotaServiceTests.FileService_CreateAsync_pri_prekroceni_kvoty_neulozi_nic</c>
    /// na úrovni služby.
    /// </remarks>
    [Fact]
    public async Task Upload_nad_kvotu_uloziste_vrati_400_s_duvodem()
    {
        // 1 bajt celkové kvóty: přeteče ji i nejmenší obrázek.
        using var factory = new TunedApiFactory(maxTotalStorageBytes: 1);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.AdminToken());

        var response = await client.PostAsync("/api/gallery/upload", ImageContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Text musí projít až ke klientovi: kvóta je jediné selhání uploadu,
        // se kterým může uživatel něco udělat. Konkrétní znění pochází
        // ze StorageQuotaService — „Nedostatek místa v úložišti (… / … MB)“.
        Assert.Contains("úložišt", body, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider
            .GetRequiredService<Demizon.Dal.DemizonContext>();
        Assert.Empty(db.Files);
    }

    [Fact]
    public async Task Upload_v_ramci_kvoty_projde()
    {
        using var factory = new TunedApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.AdminToken());

        var response = await client.PostAsync("/api/gallery/upload", ImageContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Rate limiter na <c>/api/auth/token</c>. Ostatní testy ho mají zvednutý,
    /// aby se do něj suite netrefila, takže bez vlastního hosta by 429 nikdo
    /// neověřil — a limit chrání proti hádání hesel.
    /// </summary>
    [Fact]
    public async Task Sesty_pokus_o_prihlaseni_v_okne_vrati_429()
    {
        const int limit = 3;
        using var factory = new TunedApiFactory(authPermitLimit: limit);
        using var client = factory.CreateClient();

        var badLogin = new TokenRequest(TunedApiFactory.AdminLogin, "spatne-heslo");

        for (var i = 0; i < limit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/token", badLogin);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/token", badLogin);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Rate_limiter_se_netyka_jinych_endpointu()
    {
        using var factory = new TunedApiFactory(authPermitLimit: 1);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest(TunedApiFactory.AdminLogin, "spatne-heslo"));

        // /health nemá politiku "auth", takže vyčerpané okno ho nesmí zablokovat.
        for (var i = 0; i < 3; i++)
        {
            var health = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        }
    }

    private static MultipartFormDataContent ImageContent()
    {
        var bytes = TestImages.Jpeg(200, 150);
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "test.jpg");
        return content;
    }
}
