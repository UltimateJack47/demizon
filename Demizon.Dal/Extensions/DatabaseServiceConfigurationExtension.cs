using Demizon.Dal.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Dal.Extensions;

public static class DatabaseServiceConfigurationExtension
{
    private static DbContextOptionsBuilder BuildOptions(string connectionString,
        DbContextOptionsBuilder? builder = null)
    {
        builder ??= new DbContextOptionsBuilder();

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
    /// Aktivuje WAL mode pro souběžný přístup více procesů (Mvc + Api) ke stejnému SQLite souboru.
    /// Nastavení je persistentní (uloží se do DB souboru) — stačí volat jednou při startu.
    /// busy_timeout je nakonfigurován v connection stringu přes BusyTimeout=5000 v BuildOptions.
    /// </summary>
    public static void EnableWalMode(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }
}
