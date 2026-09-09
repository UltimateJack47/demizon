using CryptoHelper;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Authentication;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Services;
using Demizon.Mvc.Services.Notification;
using Demizon.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Unit.Infrastructure;

/// <summary>
/// Host s dvojníkem kalendáře a s docházkovou službou, které jde na příkaz
/// nechat selhat zápis. Slouží k testům kompenzační logiky v
/// <c>AttendancesController</c>.
/// </summary>
public sealed class CalendarApiFactory : WebApplicationFactory<Program>
{
    public const string MemberLogin = "gcal-clen";
    public const string MemberPassword = "gcal-clen-heslo-1";

    private readonly string _dbPath = TestHostEnvironment.NewDatabasePath("gcal");

    public CalendarTestState State { get; } = new();

    public int MemberId { get; private set; }
    public int EventId { get; private set; }

    public CalendarApiFactory()
    {
        TestHostEnvironment.Apply(_dbPath);
        _ = Server;
        Seed();
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

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(State);

            // Dvojník kalendáře: skutečná služba mluví s Google API.
            services.RemoveAll<IGoogleCalendarService>();
            services.AddTransient<IGoogleCalendarService>(sp =>
                new FakeGoogleCalendarService(sp.GetRequiredService<CalendarTestState>()));

            // Obal nad skutečnou docházkovou službou — čtení projde, zápis
            // selže na příkaz. Registruje se ručně, protože obal potřebuje
            // instanci té skutečné.
            services.RemoveAll<IAttendanceService>();
            services.AddScoped<IAttendanceService>(sp => new WriteFailingAttendanceService(
                new AttendanceService(
                    sp.GetRequiredService<DemizonContext>(),
                    sp.GetRequiredService<ILogger<AttendanceService>>()),
                sp.GetRequiredService<CalendarTestState>()));
        });
    }

    private void Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();

        var member = new Member
        {
            Name = "GCal",
            Surname = "Clen",
            Login = MemberLogin,
            Email = $"{MemberLogin}@demizon.test",
            PasswordHash = PasswordHasher.HashPassword(MemberPassword),
            Role = UserRole.Admin,
            Gender = Gender.Male,
            // Propojený kalendář: bez obojího controller synchronizaci přeskočí.
            GoogleRefreshToken = "fake-refresh-token",
            GoogleCalendarId = "primary",
            GoogleConnectedAt = DateTime.UtcNow,
        };
        db.Members.Add(member);

        var ev = new Event
        {
            Name = "Testovací vystoupení",
            DateFrom = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            DateTo = new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc),
        };
        db.Events.Add(ev);

        db.SaveChanges();
        MemberId = member.Id;
        EventId = ev.Id;
    }

    /// <summary>JWT vydaný přímo službou — obchází rate limiter na /api/auth/token.</summary>
    public string MemberToken()
    {
        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        return tokens.GenerateToken(db.Members.Single(m => m.Id == MemberId));
    }

    public DemizonContext NewContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DemizonContext>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TestHostEnvironment.DeleteDatabase(_dbPath);
    }
}
