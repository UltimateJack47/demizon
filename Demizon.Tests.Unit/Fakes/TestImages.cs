using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Demizon.Tests.Unit.Fakes;

/// <summary>Generátor testovacích obrázků, aby testy nepotřebovaly binární fixtures v repu.</summary>
public static class TestImages
{
    /// <summary>
    /// JPEG s barevným přechodem. Plocha se záměrně nevyplňuje jednou barvou — konstantní
    /// obrázek se zakóduje na několik set bajtů a kompresní ani vzorkovací regrese by na něm
    /// nebyla vidět.
    /// </summary>
    public static byte[] Jpeg(int width, int height, ushort? exifOrientation = null)
    {
        using var image = Gradient(width, height);

        if (exifOrientation is { } orientation)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation);
        }

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 95 });
        return ms.ToArray();
    }

    public static byte[] Png(int width, int height)
    {
        using var image = Gradient(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms, new PngEncoder());
        return ms.ToArray();
    }

    /// <summary>Levý horní kvadrant červeně — umožňuje testu poznat, že se obrázek otočil.</summary>
    public static byte[] JpegWithRedTopLeft(int width, int height, ushort? exifOrientation = null)
    {
        // Kreslí se ručně přes pixely — Fill/rectangle je až v balíčku ImageSharp.Drawing,
        // který tento projekt (ani produkční kód) nereferencuje.
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var isTopHalf = y < accessor.Height / 2;
                for (var x = 0; x < row.Length; x++)
                {
                    var isLeftHalf = x < row.Length / 2;
                    row[x] = isTopHalf && isLeftHalf
                        ? new Rgb24(255, 0, 0)
                        : new Rgb24(255, 255, 255);
                }
            }
        });

        if (exifOrientation is { } orientation)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation);
        }

        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms, new JpegEncoder { Quality = 100 });
        return ms.ToArray();
    }

    private static Image<Rgb24> Gradient(int width, int height)
    {
        var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24(
                        (byte)(x * 255 / Math.Max(1, accessor.Width - 1)),
                        (byte)(y * 255 / Math.Max(1, accessor.Height - 1)),
                        (byte)((x + y) % 256));
                }
            }
        });
        return image;
    }
}
