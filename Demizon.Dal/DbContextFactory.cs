using Demizon.Common.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Demizon.Dal;

public class DbContextFactory : IDesignTimeDbContextFactory<DemizonContext>
{
    public DemizonContext CreateDbContext(string[] args)
    {
        
        var optionsBuilder = new DbContextOptionsBuilder<DemizonContext>();
        optionsBuilder
            .UseLazyLoadingProxies()
            .UseSqlite(DefaultConnectionString.DbConnectionString);

        return new DemizonContext(optionsBuilder.Options);
    }
}
