using Demizon.Common.Configuration;
using Demizon.Core.Services.Storage;
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
    IStorageQuotaService? storageQuota = null,
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

        uploadSessionIdentifier ??= Guid.NewGuid().ToString();
        string fileRelPathDir = $"{UploadSettings.ImagesDirectory}/{uploadSessionIdentifier}/";
        Directory.CreateDirectory(documentRoot + "/" + fileRelPathDir);

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
        var quota = await EnsureQuotaAsync(fileRequest.FileSize);
        if (quota is not null)
            return QuotaFailed(fileRequest, quota);

        using var ms = new MemoryStream();
        await fileRequest.Stream.CopyToAsync(ms);
        ms.Position = 0;

        byte[] fullData;
        byte[] thumbData;
        try
        {
            (fullData, thumbData) = OptimizeImage(ms);
        }
        catch (Exception ex) when (ex is ImageFormatException
                                       or InvalidMemoryOperationException
                                       or ImageProcessingException
                                       or NotSupportedException)
        {
            return FailedImage(fileRequest, ex);
        }

        var postQuota = await EnsureQuotaAsync(fullData.Length + (thumbData?.Length ?? 0));
        if (postQuota is not null)
            return QuotaFailed(fileRequest, postQuota);

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
        var quota = await EnsureQuotaAsync(fileRequest.FileSize);
        if (quota is not null)
            return QuotaFailed(fileRequest, quota);

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

    private async Task<string?> EnsureQuotaAsync(long bytes)
    {
        if (bytes > UploadSettings.MaxFileBytes)
        {
            var limitMb = UploadSettings.MaxFileBytes / (1024.0 * 1024.0);
            return $"Soubor přesahuje limit {limitMb:0.#} MB na soubor.";
        }

        if (storageQuota is null)
            return null;

        var (allowed, reason) = await storageQuota.EnsureCanStoreAsync(bytes);
        return allowed ? null : reason;
    }

    private static FileUploadResult QuotaFailed(FileUploadRequest fileRequest, string reason) =>
        new()
        {
            IsSuccessful = false,
            ErrorMessage = reason,
            FileName = fileRequest.FileName,
            FileExtension = fileRequest.FileExtension,
            ContentType = fileRequest.ContentType,
            RelativePath = string.Empty,
            FileSize = 0
        };

    private FileUploadResult FailedImage(FileUploadRequest fileRequest, Exception ex)
    {
        var isTooLarge = ex is InvalidMemoryOperationException
                         || ex.InnerException is InvalidMemoryOperationException;

        logger?.LogWarning(ex,
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

    private static (byte[] Full, byte[] Thumbnail) OptimizeImage(Stream source)
    {
        var info = Image.Identify(source);
        source.Position = 0;

        var options = new DecoderOptions
        {
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

        return (full, thumbnail);
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
