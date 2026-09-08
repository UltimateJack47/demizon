namespace Demizon.Core.Services.Storage;

/// <summary>
/// One disk-hygiene cycle: purge high-churn rows, then WAL truncate + incremental vacuum.
/// </summary>
public interface IDiskMaintenanceService
{
    Task RunCycleAsync(CancellationToken cancellationToken = default);
}
