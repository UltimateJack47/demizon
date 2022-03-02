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
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Borrowing> Borrowings { get; set; } = null!;

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
            b.Property(s => s.Description).HasMaxLength(255);
            b.Property(s => s.Price).HasColumnType("decimal(7,2)");
            b.Property(s => s.Year).IsRequired();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.Login).IsRequired();
        });

        modelBuilder.Entity<Borrowing>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Device)
                .WithMany(y => y.Borrowings)
                .HasForeignKey(x => x.DeviceId);
            b.HasOne(x => x.User)
                .WithMany(y => y.Borrowings)
                .HasForeignKey(x => x.UserId);
            b.Property(s => s.Start).IsRequired();
        });
    }
}
