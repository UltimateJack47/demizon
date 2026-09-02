using Demizon.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace Demizon.Core.Services.FileUpload;

public class FileUploadService(
    IOptionsSnapshot<UploadSettings> uploadSettings,
    ILogger<FileUploadService>? logger = null) : IFileUploadService
{
    private const int MaxImageWidth = 1200;
    private const int ThumbnailWidth = 200;
    private const int JpegQuality = 80;

    private UploadSettings UploadSettings { get; } = uploadSettings.Value;

    public async Task<FileUploadResult> UploadImageAsync(FileUploadRequest fileRequest,
        bool createResizedImages = false, string? uploadSessionIdentifier = null)
    {
        string documentRoot = Environment.CurrentDirectory;

        // Create directory
        uploadSessionIdentifier ??= Guid.NewGuid().ToString();
        string fileRelPathDir = $"{UploadSettings.ImagesDirectory}/{uploadSessionIdentifier}/";
        Directory.CreateDirectory(documentRoot + "/" + fileRelPathDir);

        // Set paths
        string fileName = Guid.NewGuid() + fileRequest.FileExtension;
        Uri fileUri = new Uri(documentRoot + "/" + fileRelPathDir + fileName);

        await using (var stream = new FileStream(fileUri.AbsolutePath, FileMode.Create))
        {
            await fileRequest.Stream.CopyToAsync(stream);
        }

        return new FileUploadResult
        {
            FileExtension = fileRequest.FileExtension,
            FileName = Path.GetFileNameWithoutExtension(fileName),
            RelativePath = fileRelPathDir + fileName,
            ContentType = fileRequest.ContentType,
            FileSize = fileRequest.FileSize,
            IsSuccessful = true
        };
    }

    public async Task<FileUploadResult> UploadImageToDbAsync(FileUploadRequest fileRequest)
    {
        using var ms = new MemoryStream();
        await fileRequest.Stream.CopyToAsync(ms);
        ms.Position = 0;

        byte[] fullData;
        byte[] thumbData;
        try
        {
            (fullData, thumbData) = OptimizeImage(ms);
        }
        // Výčet je záměrný, ne `catch (Exception)`: chyba ve vlastním kódu (třeba
        // v ComputeDecodeSize) má skončit 500 se stack trace, ne tichým „neplatný obrázek“.
        // Všechny čtyři typy jsou dokumentované návratové cesty ImageSharpu pro vstup,
        // který nelze dekódovat — včetně NotSupportedException, kterou hlásí Identify
        // i Load u rozpoznaných, ale nepodporovaných variant formátu (bezeztrátový
        // aritmetický JPEG, komprese BMP mimo RLE, řada variant TIFFu).
        catch (Exception ex) when (ex is ImageFormatException
                                       or InvalidMemoryOperationException
                                       or ImageProcessingException
                                       or NotSupportedException)
        {
            return FailedImage(fileRequest, ex);
        }

        return new FileUploadResult
        {
            FileExtension = ".jpg",
            FileName = Guid.NewGuid().ToString(),
            RelativePath = "db-stored",
            ContentType = "image/jpeg",
            FileSize = fullData.Length,
            Data = fullData,
            ThumbnailData = thumbData,
            IsSuccessful = true
        };
    }

    public async Task<FileUploadResult> UploadDocumentToDbAsync(FileUploadRequest fileRequest)
    {
        using var ms = new MemoryStream();
        await fileRequest.Stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        return new FileUploadResult
        {
            FileExtension = fileRequest.FileExtension,
            FileName = fileRequest.FileName,
            RelativePath = "db-stored",
            ContentType = fileRequest.ContentType,
            FileSize = bytes.Length,
            Data = bytes,
            ThumbnailData = null,
            IsSuccessful = true
        };
    }

    /// <summary>
    /// Poškozený soubor i obrázek, na který nestačí strop alokátoru ImageSharpu
    /// (<c>AllocationLimitMegabytes</c> v <c>CoreServicesRegistrationExtension</c>),
    /// jsou chyby na <b>vstupu</b>. Bez tohoto převodu by výjimka z dekodéru probublala
    /// až do controlleru a klient by dostal HTTP 500 místo srozumitelného 400.
    /// </summary>
    /// <remarks>
    /// Strop se v praxi projeví jen u formátů bez škálovaného dekódu — PNG, BMP a TIFF
    /// alokují celý raster dopředu, takže narazí kolem 35 Mpx. JPEG projde i na 100 Mpx,
    /// protože <c>DecoderOptions.TargetSize</c> u něj spustí škálovaný IDCT.
    /// </remarks>
    private FileUploadResult FailedImage(FileUploadRequest fileRequest, Exception ex)
    {
        var isTooLarge = ex is InvalidMemoryOperationException
                         || ex.InnerException is InvalidMemoryOperationException;

        // Bez tohohle záznamu by byla regrese dekodéru, useknutý multipart request
        // a legitimně příliš velká fotka v provozu nerozlišitelné — operátor by viděl
        // jen HTTP 400 s jedním ze dvou hlášení a nikde žádný stack trace.
        logger?.LogInformation(ex,
            "Zpracování obrázku {FileName}{FileExtension} ({FileSize} B) selhalo: {Reason}.",
            fileRequest.FileName, fileRequest.FileExtension, fileRequest.FileSize,
            isTooLarge ? "nad stropem alokátoru" : "nedekódovatelný vstup");

        return new FileUploadResult
        {
            IsSuccessful = false,
            ErrorMessage = isTooLarge
                ? "Obrázek má příliš mnoho pixelů na zpracování. Zmenši rozlišení a nahraj ho znovu."
                : "Soubor není platný obrázek.",
            FileName = fileRequest.FileName,
            FileExtension = fileRequest.FileExtension,
            ContentType = fileRequest.ContentType,
            RelativePath = string.Empty,
            FileSize = 0
        };
    }

    /// <summary>
    /// Dekóduje obrázek jednou a vrátí z něj plnou i náhledovou variantu jako JPEG.
    /// </summary>
    /// <remarks>
    /// Kontrakt je „strop na šířku, výška volná, nikdy nezvětšovat“ — stejně jako
    /// v původní implementaci nad ImageMagickem.
    /// <para>
    /// <see cref="DecoderOptions.TargetSize"/> se přitom vyhodnocuje jako
    /// <c>ResizeMode.Max</c>, tedy jako bounding box <em>bez</em> stropu na faktoru 1.0.
    /// Kdyby se sem předal čtverec <c>1200×1200</c>, fotka na výšku by vyšla užší než
    /// 1200 px a malá fotka by se naopak zvětšila. Proto se box počítá z poměru stran
    /// samotného zdroje a faktor se zastropuje na 1.0 — u JPEGu tím pořád zapneme
    /// škálovaný IDCT, takže se plný raster zdrojové fotky nikdy nealokuje.
    /// </para>
    /// </remarks>
    private static (byte[] Full, byte[] Thumbnail) OptimizeImage(Stream source)
    {
        // Identify čte jen hlavičku, žádný raster nealokuje.
        var info = Image.Identify(source);
        source.Position = 0;

        var options = new DecoderOptions
        {
            MaxFrames = 1,
            TargetSize = ComputeDecodeSize(info)
        };

        using var image = Image.Load(options, source);

        // EXIF orientaci je nutné promítnout do pixelů dřív, než metadata zahodíme,
        // jinak by se fotky z mobilu na výšku zobrazovaly otočené.
        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        // Pořadí je závazné: ResizeToWidth zmenšuje sdílenou instanci na místě,
        // takže plná varianta musí vzniknout před náhledem.
        var full = ResizeToWidth(image, MaxImageWidth);
        var thumbnail = ResizeToWidth(image, ThumbnailWidth);

        return (full, thumbnail);
    }

    /// <summary>
    /// Spočítá rozměry pro škálovaný dekód v <em>uložených</em> souřadnicích: zachová
    /// poměr stran zdroje a zastropuje zobrazenou šířku na <see cref="MaxImageWidth"/>.
    /// </summary>
    private static Size ComputeDecodeSize(ImageInfo info)
    {
        // Dekodér EXIF rotaci neaplikuje, takže u orientací 5–8 (rotace o 90°/270°)
        // je zobrazená šířka uloženou výškou a strop musí jít na opačnou osu.
        var storedWidthAxis = SwapsAxes(info) ? info.Height : info.Width;

        // Math.Min(1.0, …) je to, co brání upscalu malých obrázků.
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
