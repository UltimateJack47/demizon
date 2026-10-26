using Demizon.Common.Configuration;
using Demizon.Core.Services.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.FileUpload;

public class FileUploadService(
    IOptionsSnapshot<UploadSettings> uploadSettings,
    IStorageQuotaService? storageQuota = null,
    ILogger<FileUploadService>? logger = null) : IFileUploadService
{
    private UploadSettings UploadSettings { get; } = uploadSettings.Value;

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
            var optimized = ImageOptimizer.Optimize(ms);
            (fullData, thumbData) = (optimized.Full, optimized.Thumbnail);
        }
        catch (Exception ex) when (ImageOptimizer.IsDecodeFailure(ex))
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
        var isTooLarge = ImageOptimizer.IsTooLarge(ex);

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
}
