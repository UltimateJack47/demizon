using Demizon.Common.Configuration;
using Demizon.Mvc.Services;
using Demizon.Mvc.Services.Notification;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit.Infrastructure;

/// <summary>
/// Web host nad <b>prázdnou</b> databází a se zapnutým bootstrap tokenem.
/// Nesmí sdílet databázi s <see cref="AuthApiFactory"/> — seed endpoint testuje
/// právě chování na databázi bez jediného člena.
/// </summary>
public sealed class SeedApiFactory : WebApplicationFactory<Program>
{
    public const string SeedToken = "bootstrap-token-pro-test";

    private readonly string _dbPath = TestHostEnvironment.NewDatabasePath("seed");

    public SeedApiFactory()
    {
        TestHostEnvironment.Apply(_dbPath);
        // Host se staví hned, dokud env proměnné ukazují na _dbPath. Bez toho
        // by se postavil líně a mohl by se trefit do databáze jiné fixture.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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

        // PostConfigure běží po nabindování z konfigurace, takže token platí
        // jen pro tento host — na rozdíl od env proměnné, která je pro proces.
        builder.ConfigureTestServices(services =>
            services.PostConfigure<BootstrapSettings>(o => o.SeedToken = SeedToken));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TestHostEnvironment.DeleteDatabase(_dbPath);
    }
}
