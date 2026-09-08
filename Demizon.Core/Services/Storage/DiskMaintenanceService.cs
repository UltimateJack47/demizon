using Demizon.Dal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Storage;

/// <summary>
/// Purges stale AuditLog / RefreshToken / SentNotification rows and forces a WAL truncate.
/// The hosted service in MVC schedules this hourly on a short-lived scope so checkpoint
/// is not blocked by Blazor Server circuits holding a scoped <see cref="DemizonContext"/>.
/// </summary>
public sealed class DiskMaintenanceService(
    DemizonContext db,
    ILogger<DiskMaintenanceService> logger) : IDiskMaintenanceService
{
    public static readonly TimeSpan AuditLogRetention = TimeSpan.FromDays(90);
    public static readonly TimeSpan SentNotificationRetention = TimeSpan.FromDays(180);

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        await PurgeAsync(cancellationToken);
        await CheckpointAndVacuumAsync(cancellationToken);
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var auditDeleted = await db.AuditLogs
            .Where(a => a.Timestamp < now - AuditLogRetention)
            .ExecuteDeleteAsync(ct);

        var tokensDeleted = await db.RefreshTokens
            .Where(t => t.IsRevoked || t.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var notifDeleted = await db.SentNotifications
            .Where(n => n.SentAt < now - SentNotificationRetention)
            .ExecuteDeleteAsync(ct);

        if (auditDeleted > 0 || tokensDeleted > 0 || notifDeleted > 0)
        {
            logger.LogInformation(
                "Purged {Audit} audit logs, {Tokens} refresh tokens, {Notifs} sent notifications.",
                auditDeleted, tokensDeleted, notifDeleted);
        }
    }

    private async Task CheckpointAndVacuumAsync(CancellationToken ct)
    {
        // Reuse the context connection (already opened by purge). Do not Close() —
        // the short-lived DI scope disposes the context, and closing a shared
        // connection (in-memory tests, pooled connections) would drop the DB.
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Reclaims free pages only when auto_vacuum=INCREMENTAL is already active
        // on the DB file (requires one-time VACUUM after enabling — see interceptor docs).
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA incremental_vacuum(256);";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("SQLite wal_checkpoint(TRUNCATE) and incremental_vacuum completed.");
    }
}
