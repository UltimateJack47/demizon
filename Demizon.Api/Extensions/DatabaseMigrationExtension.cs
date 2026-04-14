using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Api.Extensions;

public static class DatabaseMigrationExtension
{
    public static void ApplyDbMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        dbContext.Database.Migrate();
    }
}
