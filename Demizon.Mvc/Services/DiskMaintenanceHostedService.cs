using Demizon.Core.Services.Storage;
using Demizon.Dal;

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
        await using var scope = scopeFactory.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<IDiskMaintenanceService>();
        await maintenance.RunCycleAsync(ct);
    }
}
