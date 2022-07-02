using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DomProject.Dal.Extensions;

public static class DatabaseServiceConfigurationExtension
{
    public static DbContextOptionsBuilder BuildOptions(string connectionString,
        DbContextOptionsBuilder? builder = null)
    {
        builder ??= new DbContextOptionsBuilder();

        builder
            .UseLazyLoadingProxies()
#if DEBUG
            .EnableSensitiveDataLogging()
#endif
            .UseNpgsql(connectionString);

        return builder;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DemizonContext>(options => BuildOptions(connectionString, options));
        return services;
    }
}
