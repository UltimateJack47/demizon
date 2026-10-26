using Demizon.Common;
using Demizon.Common.Configuration;
using Demizon.Core.Services.FileUpload;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.Costume;

/// <summary>
/// Fotka pro krojárku: komprese sdíleným pipeline, vlastní limit úložiště a čas pořízení z EXIF
/// převedený na UTC. Fotku jen připraví, uložení je na volajícím.
/// </summary>
public sealed class CostumePhotoProcessor(
    DemizonContext db,
    IOptions<CostumeSettings> costumeOptions,
    IOptions<GoogleCalendarSettings> calendarOptions,
    ILogger<CostumePhotoProcessor> logger)
{
    private readonly CostumeSettings _settings = costumeOptions.Value;

    /// <param name="captureSource">
    /// Začátek originálu, když <paramref name="source"/> už zmenšil klient a EXIF v něm není.
    /// </param>
    public async Task<Result<CostumePhoto>> ProcessAsync(Stream source, int uploadedById,
        Stream? captureSource = null, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        OptimizedImage optimized;
        try
        {
            optimized = ImageOptimizer.Optimize(buffer);
        }
        catch (Exception ex) when (ImageOptimizer.IsDecodeFailure(ex))
        {
            logger.LogWarning(ex, "Costume photo from member {MemberId} could not be processed.", uploadedById);
            return Result<CostumePhoto>.Rejected(ImageOptimizer.IsTooLarge(ex)
                ? "Fotka má příliš mnoho pixelů na zpracování. Zmenši rozlišení a nahraj ji znovu."
                : "Soubor není fotka ve formátu JPEG, PNG, WebP ani GIF.");
        }

        var size = optimized.Full.LongLength + optimized.Thumbnail.LongLength;
        var used = await db.CostumePhotos.SumAsync(p => (long?)p.FileSize, ct) ?? 0L;
        if (used + size > _settings.MaxPhotoStorageBytes)
        {
            logger.LogWarning("Costume photo storage is full ({Used} B of {Max} B).", used, _settings.MaxPhotoStorageBytes);
            return Result<CostumePhoto>.Rejected(
                "Úložiště fotek krojárky je plné. Dej vědět správci aplikace.");
        }

        var (capturedAt, capturedOffset) = captureSource is null
            ? (optimized.CapturedAt, optimized.CapturedOffset)
            : ImageOptimizer.ReadCaptureTime(captureSource);

        return Result<CostumePhoto>.Ok(new CostumePhoto
        {
            Data = optimized.Full,
            ThumbnailData = optimized.Thumbnail,
            FileSize = size,
            UploadedById = uploadedById,
            UploadedAt = DateTime.UtcNow,
            CapturedAt = ToUtc(capturedAt, capturedOffset),
        });
    }

    /// <summary>Fotka pořízená o víc než <see cref="CostumeSettings.StalePhotoAge"/> dřív, než byla nahrána.</summary>
    public bool IsStale(DateTime uploadedAt, DateTime? capturedAt) =>
        capturedAt is { } captured && uploadedAt - captured > _settings.StalePhotoAge;

    /// <summary>EXIF bez zóny se bere jako místní čas souboru (telefony ho tak zapisují).</summary>
    private DateTime? ToUtc(DateTime? capturedAt, TimeSpan? offset)
    {
        if (capturedAt is not { } local)
            return null;
        if (offset is { } known)
            return DateTime.SpecifyKind(local - known, DateTimeKind.Utc);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendarOptions.Value.TimeZone);
        return zone.IsInvalidTime(local)
            ? DateTime.SpecifyKind(local - zone.BaseUtcOffset, DateTimeKind.Utc)
            : TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
