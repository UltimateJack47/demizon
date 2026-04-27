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

    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<DeviceToken> DeviceTokens { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SentNotification> SentNotifications { get; set; } = null!;

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
            b.Property(m => m.IsAttendanceVisible).HasDefaultValue(true).IsRequired();
            b.Property(m => m.IsDancer).HasDefaultValue(false).IsRequired();
            b.Property(m => m.IsMusician).HasDefaultValue(false).IsRequired();
            b.Property(m => m.IsExternal).HasDefaultValue(false).IsRequired();
            b.Property(m => m.Gender).HasConversion<string>().IsRequired();
            b.HasMany(m => m.Photos)
                .WithOne(f => f.Member)
                .HasForeignKey(f => f.MemberId);
            b.HasMany(m => m.RefreshTokens)
                .WithOne(r => r.Member)
                .HasForeignKey(r => r.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(m => m.GoogleRefreshToken).HasMaxLength(512);
            b.Property(m => m.GoogleCalendarId).HasMaxLength(256);
            // Soft delete – globální filtr skryje smazané členy
            b.HasQueryFilter(m => m.DeletedAt == null);
        });

        modelBuilder.Entity<Event>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.DateFrom).IsRequired();
            b.Property(s => s.DateTo).IsRequired();
            b.Property(s => s.Recurrence).HasConversion<string>().HasDefaultValue(RecurrenceType.None);
            b.Property(s => s.IsCancelled).HasDefaultValue(false).IsRequired();
        });

        modelBuilder.Entity<File>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.DanceId);
            b.Property(s => s.Path).IsRequired();
            b.Property(s => s.IsPublic).HasDefaultValue(false).IsRequired();
            b.Property(s => s.Kind).HasConversion<int>().HasDefaultValue(FileKind.Image).IsRequired();
            b.HasIndex(x => new { x.DanceId, x.Kind });
        });

        modelBuilder.Entity<VideoLink>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.DanceId);
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
                .HasForeignKey(x => x.DanceId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasMany<VideoLink>(x => x.Videos)
                .WithOne(y => y.Dance)
                .HasForeignKey(x => x.DanceId)
                .OnDelete(DeleteBehavior.SetNull);
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.IsVisible).IsRequired();
        });


        modelBuilder.Entity<PushSubscription>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.MemberId, x.Endpoint }).IsUnique();
            b.Property(x => x.Endpoint).HasMaxLength(500).IsRequired();
            b.Property(x => x.P256dh).HasMaxLength(128).IsRequired();
            b.Property(x => x.Auth).HasMaxLength(48).IsRequired();
            b.HasOne(x => x.Member)
                .WithMany(m => m.PushSubscriptions)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Attendance>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne<Member>(x => x.Member)
                .WithMany(y => y.Attendances)
                .HasForeignKey(x => x.MemberId);
            b.HasOne<Event>(x => x.Event)
                .WithMany(y => y.Attendances)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Property(s => s.Date).IsRequired();
            b.Property(s => s.Status).HasDefaultValue(AttendanceStatus.No).HasConversion<int>().IsRequired();
            b.Property(s => s.ActivityRole).HasConversion<string>();
            b.Property(s => s.MemberId).IsRequired();
            b.Property(s => s.GoogleEventId).HasMaxLength(256);
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.Property(x => x.TokenPrefix).HasMaxLength(8).IsRequired().HasDefaultValue("");
            b.HasIndex(x => new { x.TokenPrefix, x.IsRevoked });
            b.Property(x => x.ExpiresAt).IsRequired();
        });

        modelBuilder.Entity<DeviceToken>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Token).HasMaxLength(256).IsRequired();
            b.Property(x => x.Platform).HasConversion<string>().IsRequired();
            b.HasIndex(x => x.Token).IsUnique();
            b.HasOne(x => x.Member)
                .WithMany(m => m.DeviceTokens)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            b.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
            b.Property(x => x.Action).HasMaxLength(20).IsRequired();
            b.Property(x => x.UserId).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<SentNotification>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.NotificationType).HasConversion<string>().IsRequired();
            b.Property(x => x.SentAt).IsRequired();
            b.HasIndex(x => new { x.MemberId, x.EventId, x.NotificationType });
            b.HasIndex(x => new { x.MemberId, x.RehearsalDate, x.NotificationType });
            b.HasOne(x => x.Member)
                .WithMany()
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}