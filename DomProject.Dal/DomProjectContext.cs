using DomProject.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Dal;

public class DomProjectContext : DbContext
{
    public DomProjectContext(DbContextOptions options) : base(options)
    {
    }
    
    public DbSet<Setting> Settings { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Setting>(b =>
        {
            b.HasIndex(s => s.Key)
                .IsUnique();
            b.Property(s => s.Key).HasConversion<string>();
            b.HasData(new Setting { Id = 1, Key = SettingKey.DevelopedBy, Value = "Jack", IsPublic = false });
        });
        
        modelBuilder.Entity<Device>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.Description);
            b.Property(s => s.Price).HasColumnType("decimal(7,2)");
            b.Property(s => s.Year).IsRequired();
        });
    }
}
