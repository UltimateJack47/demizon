using Demizon.Common.Configuration;
using Demizon.Core.Services.FileUpload;
using Demizon.Tests.Unit.Fakes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Demizon.Tests.Unit;

/// <summary>
/// Kontrakt obrazového pipeline: <b>strop na šířku 1200 px, výška volná, nikdy nezvětšovat</b>,
/// náhled 200 px, výstup vždy JPEG bez metadat.
/// </summary>
/// <remarks>
/// Tyto testy vznikly kvůli konkrétní regresi při přechodu z Magick.NETu na ImageSharp:
/// <c>DecoderOptions.TargetSize</c> se vyhodnocuje jako bounding box <em>bez</em> stropu na
/// faktoru 1.0, takže čtverec 1200×1200 fotky na výšku zúžil (3000×4000 → 900×1200,
/// 1000×5000 → 240×1200) a malé fotky naopak zvětšil (800×600 → 1200×900).
/// </remarks>
public class FileUploadServiceImageTests
{
    private const int MaxImageWidth = 1200;
    private const int ThumbnailWidth = 200;

    private static FileUploadService CreateService() =>
        new(new StubOptionsSnapshot<UploadSettings>(new UploadSettings
        {
            ImagesDirectory = "files/images",
            AllowedFileExtensions = [".jpg", ".jpeg", ".png", ".pdf"],
            Resize = new Dictionary<string, ResizeSettings>()
        }));

    private static async Task<FileUploadResult> UploadImageAsync(byte[] bytes, string extension = ".jpg",
        string contentType = "image/jpeg")
    {
        using var stream = new MemoryStream(bytes);
        return await CreateService().UploadImageToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileName = "photo",
            FileExtension = extension,
            ContentType = contentType,
            FileSize = bytes.Length
        });
    }

    // ---------------------------------------------------------------- rozměry

    [Theory]
    // krajina — nejběžnější případ, šířka se zastropuje
    [InlineData(4000, 3000, 1200, 900)]
    [InlineData(2400, 1600, 1200, 800)]
    // portrét z mobilu — dřív vycházel 900×1200, tedy užší než strop
    [InlineData(3000, 4000, 1200, 1600)]
    // extrémně vysoký obrázek — dřív vycházel 240×1200, tedy nepoužitelně malý
    [InlineData(2000, 10000, 1200, 6000)]
    // panorama
    [InlineData(6000, 1000, 1200, 200)]
    // čtverec
    [InlineData(2000, 2000, 1200, 1200)]
    public async Task UploadImageToDbAsync_capuje_sirku_na_1200_a_zachova_pomer_stran(
        int sourceWidth, int sourceHeight, int expectedWidth, int expectedHeight)
    {
        var result = await UploadImageAsync(TestImages.Jpeg(sourceWidth, sourceHeight));

        var full = Image.Identify(result.Data!);
        Assert.Equal(expectedWidth, full.Width);
        Assert.Equal(expectedHeight, full.Height);
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1200, 900)]
    [InlineData(200, 200)]
    [InlineData(64, 4000)]
    // Vysoký, ale dost úzký na to, aby se strop neuplatnil. Dřív z toho bylo 240×1200.
    [InlineData(1000, 5000)]
    public async Task UploadImageToDbAsync_nezvetsuje_obrazky_mensi_nez_strop(int width, int height)
    {
        var result = await UploadImageAsync(TestImages.Jpeg(width, height));

        var full = Image.Identify(result.Data!);
        Assert.Equal(width, full.Width);
        Assert.Equal(height, full.Height);
    }

    [Theory]
    [InlineData(4000, 3000, 200, 150)]
    [InlineData(3000, 4000, 200, 267)]
    [InlineData(150, 100, 150, 100)] // menší než náhled — taky se nezvětšuje
    public async Task UploadImageToDbAsync_vytvori_nahled_o_sirce_200(
        int sourceWidth, int sourceHeight, int expectedWidth, int expectedHeight)
    {
        var result = await UploadImageAsync(TestImages.Jpeg(sourceWidth, sourceHeight));

        var thumb = Image.Identify(result.ThumbnailData!);
        Assert.Equal(expectedWidth, thumb.Width);
        Assert.Equal(expectedHeight, thumb.Height);
    }

    [Fact]
    public async Task UploadImageToDbAsync_nahled_je_mensi_nez_plna_varianta()
    {
        var result = await UploadImageAsync(TestImages.Jpeg(4000, 3000));

        Assert.True(result.ThumbnailData!.Length < result.Data!.Length,
            $"náhled ({result.ThumbnailData.Length} B) musí být menší než plná varianta ({result.Data.Length} B)");
        Assert.Equal(ThumbnailWidth, Image.Identify(result.ThumbnailData).Width);
        Assert.Equal(MaxImageWidth, Image.Identify(result.Data).Width);
    }

    [Fact]
    public async Task UploadImageToDbAsync_zvlada_i_png_vstup()
    {
        var result = await UploadImageAsync(TestImages.Png(2000, 1500), ".png", "image/png");

        var full = Image.Identify(result.Data!);
        Assert.Equal(1200, full.Width);
        Assert.Equal(900, full.Height);
    }

    // ---------------------------------------------------------------- EXIF orientace

    [Theory]
    [InlineData(6)] // rotace 90° CW
    [InlineData(8)] // rotace 270° CW
    public async Task UploadImageToDbAsync_u_rotace_o_90_stupnu_capuje_zobrazenou_sirku(ushort orientation)
    {
        // Uloženo 4000×3000 na šířku, ale EXIF říká "otoč o 90°", takže se zobrazuje
        // jako 3000×4000 na výšku. Strop 1200 se musí vztahovat na zobrazenou šířku.
        var result = await UploadImageAsync(TestImages.Jpeg(4000, 3000, orientation));

        var full = Image.Identify(result.Data!);
        Assert.Equal(1200, full.Width);
        Assert.Equal(1600, full.Height);
    }

    [Theory]
    [InlineData(1)] // bez rotace
    [InlineData(3)] // rotace 180° — osy se nemění
    public async Task UploadImageToDbAsync_u_rotace_bez_zameny_os_capuje_ulozenou_sirku(ushort orientation)
    {
        var result = await UploadImageAsync(TestImages.Jpeg(4000, 3000, orientation));

        var full = Image.Identify(result.Data!);
        Assert.Equal(1200, full.Width);
        Assert.Equal(900, full.Height);
    }

    [Fact]
    public async Task UploadImageToDbAsync_promitne_exif_rotaci_do_pixelu()
    {
        // Zdroj má červený levý horní kvadrant a EXIF orientaci 6 (rotace 90° CW).
        // Po správném AutoOrient se červená plocha přesune do pravého horního kvadrantu.
        var result = await UploadImageAsync(TestImages.JpegWithRedTopLeft(800, 600, exifOrientation: 6));

        using var full = Image.Load<Rgb24>(result.Data!);
        Assert.True(IsReddish(SampleAt(full, 0.75f, 0.25f)),
            "po rotaci o 90° CW má být červená v pravém horním kvadrantu");
        Assert.False(IsReddish(SampleAt(full, 0.25f, 0.25f)),
            "levý horní kvadrant už červený být nemá");
    }

    // ---------------------------------------------------------------- výstupní formát

    [Fact]
    public async Task UploadImageToDbAsync_vraci_vzdy_jpeg_bez_ohledu_na_vstupni_format()
    {
        var result = await UploadImageAsync(TestImages.Png(1000, 800), ".png", "image/png");

        Assert.Equal(".jpg", result.FileExtension);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.IsType<JpegFormat>(Image.DetectFormat(result.Data!));
        Assert.IsType<JpegFormat>(Image.DetectFormat(result.ThumbnailData!));
    }

    [Fact]
    public async Task UploadImageToDbAsync_zahodi_exif_metadata()
    {
        var result = await UploadImageAsync(TestImages.Jpeg(2000, 1500, exifOrientation: 6));

        var full = Image.Identify(result.Data!);
        Assert.Null(full.Metadata.ExifProfile);
        Assert.Null(full.Metadata.IptcProfile);
        Assert.Null(full.Metadata.XmpProfile);
    }

    [Fact]
    public async Task UploadImageToDbAsync_vyplni_metadata_vysledku()
    {
        var result = await UploadImageAsync(TestImages.Jpeg(2000, 1500));

        Assert.True(result.IsSuccessful);
        Assert.Equal("db-stored", result.RelativePath);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.ThumbnailData);
        // FileSize musí odpovídat tomu, co se skutečně uloží, ne velikosti nahraného souboru.
        Assert.Equal(result.Data!.Length, result.FileSize);
        Assert.True(Guid.TryParse(result.FileName, out _), "FileName má být GUID");
    }

    [Fact]
    public async Task UploadImageToDbAsync_odmitne_neobrazkovy_vstup()
    {
        var garbage = "toto rozhodne neni obrazek"u8.ToArray();

        await Assert.ThrowsAnyAsync<Exception>(() => UploadImageAsync(garbage));
    }

    // ---------------------------------------------------------------- dokumenty

    [Fact]
    public async Task UploadDocumentToDbAsync_uklada_bajty_beze_zmeny()
    {
        var pdfBytes = "%PDF-1.7 fake document body"u8.ToArray();
        using var stream = new MemoryStream(pdfBytes);

        var result = await CreateService().UploadDocumentToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileName = "notovy-zapis",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSize = pdfBytes.Length
        });

        Assert.True(result.IsSuccessful);
        Assert.Equal(pdfBytes, result.Data);
        Assert.Null(result.ThumbnailData);
        Assert.Equal(".pdf", result.FileExtension);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("notovy-zapis", result.FileName);
        Assert.Equal(pdfBytes.Length, result.FileSize);
    }

    // ---------------------------------------------------------------- helpers

    private static Rgb24 SampleAt(Image<Rgb24> image, float relativeX, float relativeY) =>
        image[(int)(image.Width * relativeX), (int)(image.Height * relativeY)];

    private static bool IsReddish(Rgb24 pixel) => pixel.R > 150 && pixel.G < 100 && pixel.B < 100;
}
