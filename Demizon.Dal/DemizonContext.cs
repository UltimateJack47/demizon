using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using File = Demizon.Dal.Entities.File;

namespace Demizon.Dal;

public class DemizonContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Setting> Settings { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<File> Files { get; set; } = null!;
    public DbSet<VideoLink> VideoLinks { get; set; } = null!;
    public DbSet<Dance> Dances { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Setting>(b =>
        {
            b.HasIndex(s => s.Key)
                .IsUnique();
            b.Property(s => s.Key).HasConversion<string>();
            b.HasData(new Setting { Id = 1, Key = SettingKey.DevelopedBy, Value = "Jack", IsPublic = false });
        });

        modelBuilder.Entity<Member>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(m => m.Surname).IsRequired();
            b.Property(s => s.Login).IsRequired();
            b.Property(s => s.Role).HasConversion<string>().IsRequired();
            b.Property(m => m.IsVisible).IsRequired();
            b.Property(m => m.Gender).HasConversion<string>().IsRequired();
            b.HasMany(m => m.Photos)
                .WithOne(f => f.Member)
                .HasForeignKey(f => f.MemberId);
        });
        
        modelBuilder.Entity<Event>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.DateFrom).IsRequired();
            b.Property(s => s.DateTo).IsRequired();
        });

        modelBuilder.Entity<File>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Path).IsRequired();
        });

        modelBuilder.Entity<VideoLink>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.Url).IsRequired();
            b.Property(s => s.Year).IsRequired();
            b.Property(s => s.IsVisible).IsRequired();
        });

        modelBuilder.Entity<Dance>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.IsVisible).IsRequired();
        });
    }
}
