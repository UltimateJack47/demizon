using Demizon.Common.Configuration;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Demizon.Core.Services.FileUpload;

public class FileUploadService(IOptionsSnapshot<UploadSettings> uploadSettings) : IFileUploadService
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

        var (fullData, thumbData) = OptimizeImage(ms);

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
    /// Dekóduje obrázek jednou a vrátí z něj plnou i náhledovou variantu jako JPEG.
    /// <see cref="DecoderOptions.TargetSize"/> u JPEGu spustí škálovaný IDCT, takže se
    /// plný raster zdrojové fotky nikdy nealokuje.
    /// </summary>
    private static (byte[] Full, byte[] Thumbnail) OptimizeImage(Stream source)
    {
        var options = new DecoderOptions
        {
            TargetSize = new Size(MaxImageWidth, MaxImageWidth),
            MaxFrames = 1
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
