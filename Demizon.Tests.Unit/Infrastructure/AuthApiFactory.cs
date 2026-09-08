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
/// Isolovaný web host nad souborovou SQLite. Env proměnné se nastavují
/// v konstruktoru, protože <c>Program.cs</c> čte connection string a JWT
/// ještě před <c>ConfigureWebHost</c> — po <c>AddEnvironmentVariables()</c>
/// ale env vyhraje nad json soubory.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = TestHostEnvironment.JwtSecret;
    public const string StandardLogin = "clen";
    public const string StandardPassword = "spravne-heslo-1";
    public const string AdminLogin = "spravce";
    public const string AdminPassword = "spravce-heslo-1";

    private readonly string _dbPath = TestHostEnvironment.NewDatabasePath("auth");
    private readonly object _seedGate = new();
    private bool _seeded;

    public int StandardMemberId { get; private set; }
    public int AdminMemberId { get; private set; }

    public AuthApiFactory()
    {
        TestHostEnvironment.Apply(_dbPath);
        // Host se staví hned, dokud env proměnné ukazují na _dbPath — viz
        // komentář v TestHostEnvironment. Jinak by ho mohla přebít jiná fixture.
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
    }

    public void EnsureSeeded()
    {
        lock (_seedGate)
        {
            if (_seeded) return;
            StandardMemberId = SeedMember(StandardLogin, StandardPassword);
            AdminMemberId = SeedMember(AdminLogin, AdminPassword, UserRole.Admin);
            _seeded = true;
        }
    }

    public int SeedMember(
        string login,
        string password,
        UserRole role = UserRole.Standard,
        bool isExternal = false,
        DateTime? deletedAt = null)
    {
        _ = Server;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        var member = new Member
        {
            Name = login,
            Surname = "Test",
            Login = login,
            Email = $"{login}@demizon.test",
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = role,
            Gender = Gender.Male,
            IsExternal = isExternal,
            DeletedAt = deletedAt
        };
        db.Members.Add(member);
        db.SaveChanges();
        return member.Id;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TestHostEnvironment.DeleteDatabase(_dbPath);
    }
}
