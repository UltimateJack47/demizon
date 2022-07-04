using Demizon.Common.Configuration;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Migrations;

internal static class Program
{
    private static async Task Main()
    {
        // TODO: figure out how to get connection string correctly
        var builder = DatabaseServiceConfigurationExtension.BuildOptions(DefaultConnectionString.DbConnectionString);

        await using var context = new DemizonContext(builder.Options);
        await context.Database.MigrateAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
