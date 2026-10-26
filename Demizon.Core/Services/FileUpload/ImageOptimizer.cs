using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace Demizon.Core.Services.FileUpload;

/// <summary>Výsledek optimalizace: JPEG na šířku nejvýš 1200 px, náhled 200 px, bez metadat.</summary>
/// <param name="CapturedAt">
/// EXIF <c>DateTimeOriginal</c> jako čas na hodinách fotoaparátu (bez zóny); null, když chybí.
/// </param>
/// <param name="CapturedOffset">EXIF <c>OffsetTimeOriginal</c>, pokud ho fotoaparát zapsal.</param>
public sealed record OptimizedImage(byte[] Full, byte[] Thumbnail, DateTime? CapturedAt, TimeSpan? CapturedOffset);

/// <summary>
/// Sdílený obrazový pipeline pro galerii (<see cref="FileUploadService"/>) i fotky z krojárky.
/// Kvótu ani úložiště neřeší — to je věc volajícího.
/// </summary>
public static class ImageOptimizer
{
    private const int MaxImageWidth = 1200;
    private const int ThumbnailWidth = 200;
    private const int JpegQuality = 80;

    /// <summary>Výjimky, které znamenají „tohle není obrázek, který umíme“, ne chybu serveru.</summary>
    public static bool IsDecodeFailure(Exception ex) =>
        ex is ImageFormatException or InvalidMemoryOperationException or ImageProcessingException
            or NotSupportedException;

    /// <summary>Zpracování překročilo strop alokátoru — obrázek má na nás moc pixelů.</summary>
    public static bool IsTooLarge(Exception ex) =>
        ex is InvalidMemoryOperationException || ex.InnerException is InvalidMemoryOperationException;

    /// <summary>
    /// Only the formats phones and browsers actually upload. TIFF is left out on purpose: ImageSharp
    /// 3.1.x has an unpatched BigTIFF decoder hang (GHSA-wmxv-xphr-5c9g) and the fix ships only in the
    /// licence-checked 3.2+. Built per call so it picks up the allocator cap set on
    /// <see cref="Configuration.Default"/> at startup.
    /// </summary>
    private static Configuration UploadConfiguration() =>
        new(new JpegConfigurationModule(), new PngConfigurationModule(),
            new WebpConfigurationModule(), new GifConfigurationModule())
        {
            MemoryAllocator = Configuration.Default.MemoryAllocator
        };

    /// <summary>Výjimky viz <see cref="IsDecodeFailure"/>; stream musí jít přetočit na začátek.</summary>
    public static OptimizedImage Optimize(Stream source)
    {
        var configuration = UploadConfiguration();
        var info = Image.Identify(new DecoderOptions { Configuration = configuration }, source);
        source.Position = 0;

        var options = new DecoderOptions
        {
            Configuration = configuration,
            MaxFrames = 1,
            TargetSize = ComputeDecodeSize(info)
        };

        using var image = Image.Load(options, source);

        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        var full = ResizeToWidth(image, MaxImageWidth);
        var thumbnail = ResizeToWidth(image, ThumbnailWidth);

        var (capturedAt, capturedOffset) = ReadCaptureTime(info.Metadata.ExifProfile);
        return new OptimizedImage(full, thumbnail, capturedAt, capturedOffset);
    }

    private static (DateTime? At, TimeSpan? Offset) ReadCaptureTime(ExifProfile? exif)
    {
        if (exif is null
            || !exif.TryGetValue(ExifTag.DateTimeOriginal, out var original)
            || !DateTime.TryParseExact(original.Value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var at))
            return (null, null);

        TimeSpan? offset = null;
        if (exif.TryGetValue(ExifTag.OffsetTimeOriginal, out var rawOffset)
            && TimeSpan.TryParseExact(rawOffset.Value?.TrimStart('+'), @"hh\:mm", CultureInfo.InvariantCulture,
                out var parsed))
        {
            offset = rawOffset.Value!.StartsWith('-') ? parsed.Negate() : parsed;
        }

        return (DateTime.SpecifyKind(at, DateTimeKind.Unspecified), offset);
    }

    private static Size ComputeDecodeSize(ImageInfo info)
    {
        var storedWidthAxis = SwapsAxes(info) ? info.Height : info.Width;
        var scale = Math.Min(1.0, (double)MaxImageWidth / storedWidthAxis);

        return new Size(
            Math.Max(1, (int)Math.Round(info.Width * scale)),
            Math.Max(1, (int)Math.Round(info.Height * scale)));
    }

    private static bool SwapsAxes(ImageInfo info) =>
        info.Metadata.ExifProfile is { } exif
        && exif.TryGetValue(ExifTag.Orientation, out var orientation)
        && orientation.Value is 5 or 6 or 7 or 8;

    private static byte[] ResizeToWidth(Image image, int maxWidth)
    {
        if (image.Width > maxWidth)
        {
            var newHeight = (int)Math.Round(image.Height * ((double)maxWidth / image.Width));
            image.Mutate(x => x.Resize(maxWidth, Math.Max(1, newHeight)));
        }

        using var output = new MemoryStream();
        image.SaveAsJpeg(output, new JpegEncoder { Quality = JpegQuality });
        return output.ToArray();
    }
}
