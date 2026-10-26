using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using File = Demizon.Dal.Entities.File;

namespace Demizon.Dal;

public class DemizonContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Setting> Settings { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<MemberRole> MemberRoles { get; set; } = null!;
    public DbSet<File> Files { get; set; } = null!;
    public DbSet<VideoLink> VideoLinks { get; set; } = null!;
    public DbSet<Dance> Dances { get; set; } = null!;
    public DbSet<Attendance> Attendances { get; set; } = null!;

    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<DeviceToken> DeviceTokens { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SentNotification> SentNotifications { get; set; } = null!;
    public DbSet<RehearsalCancellation> RehearsalCancellations { get; set; } = null!;

    public DbSet<Costume> Costumes { get; set; } = null!;
    public DbSet<CostumePart> CostumeParts { get; set; } = null!;
    public DbSet<CostumeItem> CostumeItems { get; set; } = null!;
    public DbSet<CostumeHolding> CostumeHoldings { get; set; } = null!;
    public DbSet<CostumeTransfer> CostumeTransfers { get; set; } = null!;
    public DbSet<CostumeTransferLine> CostumeTransferLines { get; set; } = null!;
    public DbSet<CostumePhoto> CostumePhotos { get; set; } = null!;
    public DbSet<CostumeMaintenance> CostumeMaintenances { get; set; } = null!;
    public DbSet<CostumeInventoryCheck> CostumeInventoryChecks { get; set; } = null!;
    public DbSet<CostumeInventoryEntry> CostumeInventoryEntries { get; set; } = null!;

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
            b.Property(m => m.CostumeQrToken).HasMaxLength(64);
            b.HasIndex(m => m.CostumeQrToken).IsUnique();
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

        modelBuilder.Entity<MemberRole>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
            b.HasIndex(x => new { x.MemberId, x.Role }).IsUnique();
            b.HasOne(x => x.Member)
                .WithMany(m => m.Roles)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
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
            b.HasIndex(x => x.Timestamp);
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

        modelBuilder.Entity<RehearsalCancellation>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.DateFrom).IsRequired();
            b.Property(x => x.DateTo).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(RehearsalCancellationReasonMaxLength);
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.DateFrom, x.DateTo });
            // Výjimka přežije i smazání člena, který ji zadal.
            b.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        ConfigureCostumes(modelBuilder);
    }

    /// <summary>
    /// Krojárka. Na členy vedou jen cizí klíče bez navigací (viz <see cref="CostumeHolding"/>),
    /// mazání je všude Restrict: historie předávek musí přežít cokoli kromě vědomého zásahu.
    /// </summary>
    private static void ConfigureCostumes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Costume>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(CostumeNameMaxLength).IsRequired();
            b.Property(x => x.Description).HasMaxLength(CostumeTextMaxLength);
        });

        modelBuilder.Entity<CostumePart>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(CostumeNameMaxLength).IsRequired();
            b.Property(x => x.Description).HasMaxLength(CostumeTextMaxLength);
            b.Property(x => x.Gender).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.TrackingMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Costume)
                .WithMany(c => c.Parts)
                .HasForeignKey(x => x.CostumeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CostumeItem>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(CostumeCodeMaxLength).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Size).HasMaxLength(50);
            b.Property(x => x.Color).HasMaxLength(50);
            b.Property(x => x.Note).HasMaxLength(CostumeTextMaxLength);
            b.Property(x => x.State).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Part)
                .WithMany(p => p.Items)
                .HasForeignKey(x => x.PartId)
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_CostumeItems_Quantity", "Quantity > 0"));
        });

        modelBuilder.Entity<CostumeHolding>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ItemId, x.MemberId }).IsUnique();
            b.HasIndex(x => x.MemberId);
            b.HasOne(x => x.Item)
                .WithMany(i => i.Holdings)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_CostumeHoldings_Quantity", "Quantity > 0"));
        });

        modelBuilder.Entity<CostumeTransfer>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.Note).HasMaxLength(CostumeTextMaxLength);
            b.Property(x => x.RejectionReason).HasMaxLength(CostumeTextMaxLength);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.FromMemberId);
            b.HasIndex(x => x.ToMemberId);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.FromMemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.ToMemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                // null = krojárka; z krojárky do krojárky ani od člena k témuž členovi se nepředává.
                t.HasCheckConstraint("CK_CostumeTransfers_Parties",
                    "(FromMemberId IS NOT NULL OR ToMemberId IS NOT NULL) " +
                    "AND (FromMemberId IS NULL OR ToMemberId IS NULL OR FromMemberId <> ToMemberId)");
            });
        });

        modelBuilder.Entity<CostumeTransferLine>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.ConditionNote).HasMaxLength(CostumeTextMaxLength);
            b.HasIndex(x => new { x.TransferId, x.ItemId }).IsUnique();
            b.HasOne(x => x.Transfer)
                .WithMany(t => t.Lines)
                .HasForeignKey(x => x.TransferId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_CostumeTransferLines_Quantity", "Quantity > 0"));
        });

        modelBuilder.Entity<CostumePhoto>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Data).IsRequired();
            b.Property(x => x.ThumbnailData).IsRequired();
            b.HasOne(x => x.TransferLine)
                .WithMany(l => l.Photos)
                .HasForeignKey(x => x.TransferLineId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Part)
                .WithMany()
                .HasForeignKey(x => x.PartId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_CostumePhotos_Owner",
                "(TransferLineId IS NULL) <> (PartId IS NULL)"));
        });

        modelBuilder.Entity<CostumeMaintenance>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.Note).HasMaxLength(CostumeTextMaxLength);
            b.HasIndex(x => x.ItemId);
            b.HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CostumeInventoryCheck>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne<Member>().WithMany().HasForeignKey(x => x.StartedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CostumeInventoryEntry>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.CheckId, x.ItemId }).IsUnique();
            b.HasOne(x => x.Check)
                .WithMany(c => c.Entries)
                .HasForeignKey(x => x.CheckId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Item)
                .WithMany()
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public const int RehearsalCancellationReasonMaxLength = 200;
    public const int CostumeNameMaxLength = 100;
    public const int CostumeCodeMaxLength = 32;
    public const int CostumeTextMaxLength = 1000;
}
