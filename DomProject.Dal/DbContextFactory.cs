using DomProject.Common.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DomProject.Dal;

public class DbContextFactory : IDesignTimeDbContextFactory<DomProjectContext>
{
    public DomProjectContext CreateDbContext(string[] args)
    {
        
        var optionsBuilder = new DbContextOptionsBuilder<DomProjectContext>();
        optionsBuilder
            .UseLazyLoadingProxies()
            .UseNpgsql(DefaultConnectionString.DbConnectionString);

        return new DomProjectContext(optionsBuilder.Options);
    }
}
