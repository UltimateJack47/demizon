using Demizon.Mvc.Services;
using Demizon.Mvc.Services.Notification;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit;

/// <summary>
/// Za master nginx dorazí každý požadavek do Kestrelu jako http. Appka se
/// o HTTPS dozví jen z <c>X-Forwarded-Proto</c> — bez <c>UseForwardedHeaders</c>
/// by se HSTS nikdy neposlalo a <c>UseHttpsRedirection</c> by nevěděl, kam vést.
/// </summary>
/// <remarks>
/// HSTS je vidět jen mimo Development a mimo <c>localhost</c> (ten má
/// <c>HstsMiddleware</c> ve výjimkách), proto vlastní host v prostředí
/// <c>Staging</c> a adresa <c>demizon.cz</c>. Production se použít nedá:
/// <c>Program.cs</c> v ní čeká na zapisovatelný <c>/data</c>.
/// </remarks>
[Collection("WebHost")]
public class ForwardedHeadersTests
{
    private const string Hsts = "Strict-Transport-Security";

    [Fact]
    public async Task Za_proxy_s_X_Forwarded_Proto_https_posle_HSTS()
    {
        using var factory = new StagingApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://demizon.cz"),
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}");
        Assert.True(response.Headers.Contains(Hsts), "Za proxy s HTTPS chybí HSTS hlavička.");
    }

    [Fact]
    public async Task Bez_X_Forwarded_Proto_je_pozadavek_http_a_HSTS_se_neposila()
    {
        using var factory = new StagingApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://demizon.cz"),
        });

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}");
        Assert.False(response.Headers.Contains(Hsts));
    }

    private sealed class StagingApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = TestHostEnvironment.NewDatabasePath("staging");

        public StagingApiFactory()
        {
            TestHostEnvironment.Apply(_dbPath);
            _ = Server;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ImplementationType == typeof(UnifiedNotificationService)
                                         || d.ImplementationType == typeof(DiskMaintenanceHostedService))
                             .ToList())
                {
                    services.Remove(descriptor);
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            TestHostEnvironment.DeleteDatabase(_dbPath);
        }
    }
}
