using Demizon.Dal.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Demizon.Dal.Extensions;

public static class DatabaseServiceConfigurationExtension
{
    private static DbContextOptionsBuilder BuildOptions(string connectionString,
        DbContextOptionsBuilder? builder = null)
    {
        builder ??= new DbContextOptionsBuilder();

        // SQLite only
        builder
            .UseLazyLoadingProxies()
#if DEBUG
            .EnableSensitiveDataLogging()
#endif
            .UseSqlite(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));

        return builder;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        // AddDbContext (ne Pool) – nutné pro Scoped interceptor AuditSaveChangesInterceptor
        // který potřebuje ICurrentUserAccessor z HTTP kontextu.
        // SqliteBusyTimeoutInterceptor je Singleton – nastavuje busy_timeout na každém novém spojení.
        services.AddSingleton<SqliteBusyTimeoutInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<DemizonContext>((sp, options) =>
        {
            BuildOptions(connectionString, options);
            options.AddInterceptors(
                sp.GetRequiredService<SqliteBusyTimeoutInterceptor>(),
                sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        return services;
    }

    /// <summary>
    /// Aktivuje WAL mode pro SQLite s retry logikou (čeká na volume mount).
    /// Railway volumes se mountují během startu — retry zajistí, že se DB otevře.
    /// </summary>
    public static void EnableWalMode(this IServiceProvider services)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("DatabaseServiceConfiguration");
        int maxRetries = 15;
        int delayMs = 3000; // 3 sekundy mezi pokusy (Railway volume mount timing)

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
                db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                logger?.LogInformation("WAL mode enabled successfully on attempt {Attempt}", attempt);
                return; // Success!
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger?.LogWarning(ex, "WAL mode attempt {Attempt}/{MaxRetries} failed. Retrying in {DelayMs}ms...",
                    attempt, maxRetries, delayMs);
                Thread.Sleep(delayMs);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "WAL mode failed after {MaxRetries} attempts. Continuing anyway.", maxRetries);
                // Pokud ani po retry neuspěje, aplikace běží dál — WAL mode se aktivuje automaticky později
                break;
            }
        }
    }
}