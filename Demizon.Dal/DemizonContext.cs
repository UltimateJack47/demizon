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
    public DbSet<Attendance> Attendances { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Setting>(b =>
        {
            b.HasIndex(s => s.Key)
                .IsUnique();
            b.Property(s => s.Key).HasConversion<string>();
            b.HasData(new Setting {Id = 1, Key = SettingKey.DevelopedBy, Value = "Jack", IsPublic = false});
        });

        modelBuilder.Entity<Member>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).HasMaxLength(100).IsRequired();
            b.Property(m => m.Surname).HasMaxLength(100).IsRequired();
            b.Property(s => s.Login).HasMaxLength(100).IsRequired();
            b.Property(s => s.Email).HasMaxLength(100);
            b.Property(s => s.PasswordHash).HasMaxLength(150);
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
            b.HasMany<File>(x => x.Files)
                .WithOne(y => y.Dance)
                .HasForeignKey(x => x.DanceId);
            b.HasMany<VideoLink>(x => x.Videos)
                .WithOne(y => y.Dance)
                .HasForeignKey(x => x.DanceId);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.IsVisible).IsRequired();
        });

        modelBuilder.Entity<Attendance>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne<Member>(x => x.Member)
                .WithMany(y => y.Attendances)
                .HasForeignKey(x => x.MemberId);
            b.HasOne<Event>(x => x.Event)
                .WithMany(y => y.Attendances)
                .HasForeignKey(x => x.EventId);
            b.Property(s => s.Date).IsRequired();
            b.Property(s => s.Attends).HasDefaultValue(false).IsRequired();
            b.Property(s => s.MemberId).IsRequired();
        });
    }
}