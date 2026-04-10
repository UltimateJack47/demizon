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
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<DemizonContext>((sp, options) =>
        {
            BuildOptions(connectionString, options);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        return services;
    }
}
