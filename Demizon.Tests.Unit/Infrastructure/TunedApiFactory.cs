using CryptoHelper;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Services;
using Demizon.Mvc.Services.Notification;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit.Infrastructure;

/// <summary>
/// Host s vlastními limity — kvóta na úložiště a rate limiter. Obojí se čte
/// při registraci služeb, ne přes <c>IOptionsSnapshot</c>, takže je nejde
/// přenastavit u už postaveného hosta a testy si musí postavit vlastní.
/// </summary>
/// <remarks>
/// Vytváří se <b>uvnitř testu</b>, ne jako collection fixture: mění proměnné
/// prostředí procesu, a protože si host staví hned v konstruktoru, ostatní
/// (dávno postavené) hosty to neovlivní. Kolekce „WebHost“ zajišťuje, že se
/// to neděje paralelně.
/// </remarks>
public sealed class TunedApiFactory : WebApplicationFactory<Program>
{
    public const string AdminLogin = "tuned-admin";
    public const string AdminPassword = "tuned-admin-heslo-1";

    private readonly string _dbPath = TestHostEnvironment.NewDatabasePath("tuned");

    public TunedApiFactory(int authPermitLimit = 10000, long? maxTotalStorageBytes = null)
    {
        TestHostEnvironment.Apply(_dbPath, authPermitLimit, maxTotalStorageBytes);
        _ = Server;
        SeedAdmin();
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
    }

    public int AdminMemberId { get; private set; }

    private void SeedAdmin()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        var admin = new Member
        {
            Name = AdminLogin,
            Surname = "Test",
            Login = AdminLogin,
            Email = $"{AdminLogin}@demizon.test",
            PasswordHash = PasswordHasher.HashPassword(AdminPassword),
            Role = UserRole.Admin,
            Gender = Gender.Male,
        };
        db.Members.Add(admin);
        db.SaveChanges();
        AdminMemberId = admin.Id;
    }

    /// <summary>JWT vydaný přímo službou — obchází rate limiter na /api/auth/token.</summary>
    public string AdminToken()
    {
        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider
            .GetRequiredService<Demizon.Core.Services.Authentication.TokenService>();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        return tokens.GenerateToken(db.Members.Single(m => m.Id == AdminMemberId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TestHostEnvironment.DeleteDatabase(_dbPath);
        // Limity se vrací na výchozí, aby je nezdědil host postavený později.
        TestHostEnvironment.Apply(TestHostEnvironment.NewDatabasePath("reset"));
    }
}
