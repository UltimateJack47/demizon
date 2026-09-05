using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services;

/// <summary>
/// Hourly disk hygiene for Stardust: purge high-churn tables and force a WAL truncate.
/// Uses short-lived scoped <see cref="DemizonContext"/> instances so checkpoint can
/// complete without waiting for Blazor Server circuits to release their scoped contexts
/// (no <c>AddDbContextFactory</c> required).
/// </summary>
public sealed class DiskMaintenanceHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DiskMaintenanceHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan AuditLogRetention = TimeSpan.FromDays(90);
    private static readonly TimeSpan SentNotificationRetention = TimeSpan.FromDays(180);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DiskMaintenanceHostedService started.");

        // Let the app finish migrations / WAL enable before we touch the DB.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        await RunCycleAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Disk maintenance cycle failed.");
            }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        await PurgeAsync(ct);
        await CheckpointAndVacuumAsync(ct);
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        var now = DateTime.UtcNow;

        var auditCutoff = now - AuditLogRetention;
        var auditDeleted = await db.AuditLogs
            .Where(a => a.Timestamp < auditCutoff)
            .ExecuteDeleteAsync(ct);

        var tokensDeleted = await db.RefreshTokens
            .Where(t => t.IsRevoked || t.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var notifCutoff = now - SentNotificationRetention;
        var notifDeleted = await db.SentNotifications
            .Where(n => n.SentAt < notifCutoff)
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
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();

        // Open a dedicated connection so we are not blocked by long-lived Blazor scopes.
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
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
        finally
        {
            await connection.CloseAsync();
        }
    }
}
