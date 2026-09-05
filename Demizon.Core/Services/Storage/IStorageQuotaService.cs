namespace Demizon.Core.Services.Storage;

public interface IStorageQuotaService
{
    /// <summary>
    /// Returns whether <paramref name="additionalBytes"/> can be stored under configured quotas.
    /// </summary>
    Task<(bool Allowed, string? Reason)> EnsureCanStoreAsync(long additionalBytes, CancellationToken cancellationToken = default);
}
