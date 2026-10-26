using Demizon.Common.Configuration;
using Demizon.Core.Services.Costume;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Demizon.Tests.Integration.Infrastructure;

public static class CostumeTestData
{
    public static CostumePhotoProcessor PhotoProcessor(DemizonContext db, CostumeSettings? settings = null) =>
        new(db, Options.Create(settings ?? new CostumeSettings()),
            Options.Create(new GoogleCalendarSettings { RedirectUri = "http://localhost", TimeZone = "Europe/Prague" }),
            NullLogger<CostumePhotoProcessor>.Instance);

    public static CostumeTransferService TransferService(DemizonContext db, CostumeSettings? settings = null) =>
        new(db, PhotoProcessor(db, settings), NullLogger<CostumeTransferService>.Instance);

    public static CostumeCatalogService CatalogService(DemizonContext db) =>
        new(db, NullLogger<CostumeCatalogService>.Instance);

    public static async Task<CostumeItem> SeedItemAsync(DemizonContext db, string code,
        CostumeTrackingMode mode = CostumeTrackingMode.Individual, int quantity = 1)
    {
        var part = await db.CostumeParts.FirstOrDefaultAsync(p => p.TrackingMode == mode)
                   ?? new CostumePart
                   {
                       Name = mode == CostumeTrackingMode.Individual ? "Vesta" : "Stuha",
                       Gender = CostumeGender.Unisex,
                       TrackingMode = mode,
                   };
        var item = new CostumeItem { Part = part, Code = code, Quantity = quantity };
        db.CostumeItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public static async Task HoldAsync(DemizonContext db, int itemId, int memberId, int quantity = 1)
    {
        db.CostumeHoldings.Add(new CostumeHolding { ItemId = itemId, MemberId = memberId, Quantity = quantity });
        await db.SaveChangesAsync();
    }

    /// <param name="capturedAt">Zapíše se jako EXIF <c>DateTimeOriginal</c> (čas na hodinách fotoaparátu).</param>
    public static MemoryStream Jpeg(DateTime? capturedAt = null, string? offset = null)
    {
        using var image = new Image<Rgb24>(32, 24);
        if (capturedAt is { } at)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.DateTimeOriginal, at.ToString("yyyy:MM:dd HH:mm:ss"));
            if (offset is not null)
                image.Metadata.ExifProfile.SetValue(ExifTag.OffsetTimeOriginal, offset);
        }

        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        return stream;
    }
}
