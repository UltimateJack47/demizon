using DomProject.Common.Configuration;
using DomProject.Dal;
using DomProject.Dal.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Migrations;

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
