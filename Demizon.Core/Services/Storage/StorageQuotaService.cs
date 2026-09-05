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
            return (false, $"Soubor p\u0159esahuje limit {limitMb:0.#} MB na soubor.");
        }

        var count = await db.Files.CountAsync(cancellationToken);
        if (count >= settings.MaxFileCount)
        {
            return (false,
                $"Dosa\u017een limit po\u010dtu soubor\u016f ({settings.MaxFileCount}). Sma\u017e star\u00e9 fotky/dokumenty.");
        }

        var used = await db.Files.SumAsync(f => (long?)f.FileSize, cancellationToken) ?? 0L;
        if (used + additionalBytes > settings.MaxTotalStorageBytes)
        {
            var usedMb = used / (1024.0 * 1024.0);
            var maxMb = settings.MaxTotalStorageBytes / (1024.0 * 1024.0);
            return (false,
                $"Nedostatek m\u00edsta v \u00falo\u017ei\u0161ti ({usedMb:0.#} / {maxMb:0.#} MB). Sma\u017e star\u00e9 soubory.");
        }

        return (true, null);
    }
}
