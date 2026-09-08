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
    public const string JwtSecret = "test-jwt-secret-key-32chars-min!";
    public const string StandardLogin = "clen";
    public const string StandardPassword = "spravne-heslo-1";
    public const string AdminLogin = "spravce";
    public const string AdminPassword = "spravce-heslo-1";

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"demizon-auth-{Guid.NewGuid():N}.sqlite");
    private readonly object _seedGate = new();
    private bool _seeded;

    public int StandardMemberId { get; private set; }
    public int AdminMemberId { get; private set; }

    public AuthApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={_dbPath}");
        Environment.SetEnvironmentVariable("Jwt__SecretKey", JwtSecret);
        Environment.SetEnvironmentVariable("Vapid__PublicKey", "test-vapid-public");
        Environment.SetEnvironmentVariable("Vapid__PrivateKey", "test-vapid-private");
        Environment.SetEnvironmentVariable("Vapid__Subject", "mailto:test@demizon.test");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "10000");
        Environment.SetEnvironmentVariable("AllowedHosts", "*");
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
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { System.IO.File.Delete(path); }
            catch (IOException) { /* still locked on some hosts; temp will GC */ }
        }
    }
}
