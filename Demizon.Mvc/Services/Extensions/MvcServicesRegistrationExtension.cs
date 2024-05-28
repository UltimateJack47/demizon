using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services.Extensions;

public static class MvcServicesRegistrationExtension
{
    /// <summary>
    /// Collection of used services in the Api
    /// </summary>
    /// <param name="services">Collection of used services</param>
    /// <returns>Services that are used in the Api</returns>
    public static IServiceCollection AddMvcServices(this IServiceCollection services)
    {
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        services.AddTransient<PageService>();

        return services;
    }

    public static void ApplyDbMigrations(this IApplicationBuilder app)
    {
        using var services = app.ApplicationServices.CreateScope();
        var dbContext = services.ServiceProvider.GetRequiredService<DemizonContext>();
        dbContext.Database.Migrate();
    }
}
