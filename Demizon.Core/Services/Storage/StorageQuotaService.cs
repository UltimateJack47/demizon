using Demizon.Common.Configuration;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.Storage;

public class StorageQuotaService(
    DemizonContext db,
    IOptionsSnapshot<UploadSettings> uploadSettings) : IStorageQuotaService
{
    public async Task<(bool Allowed, string? Reason)> EnsureCanStoreAsync(
        long additionalBytes, CancellationToken cancellationToken = default)
    {
        var settings = uploadSettings.Value;

        if (additionalBytes > settings.MaxFileBytes)
        {
            var limitMb = settings.MaxFileBytes / (1024.0 * 1024.0);
            return (false, $"Soubor přesahuje limit {limitMb:0.#} MB na soubor.");
        }

        var count = await db.Files.CountAsync(cancellationToken);
        if (count >= settings.MaxFileCount)
        {
            return (false,
                $"Dosažen limit počtu souborů ({settings.MaxFileCount}). Smaž staré fotky/dokumenty.");
        }

        var used = await db.Files.SumAsync(f => (long?)f.FileSize, cancellationToken) ?? 0L;
        if (used + additionalBytes > settings.MaxTotalStorageBytes)
        {
            var usedMb = used / (1024.0 * 1024.0);
            var maxMb = settings.MaxTotalStorageBytes / (1024.0 * 1024.0);
            return (false,
                $"Nedostatek místa v úložišti ({usedMb:0.#} / {maxMb:0.#} MB). Smaž staré soubory.");
        }

        return (true, null);
    }
}
