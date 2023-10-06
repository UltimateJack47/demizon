namespace Demizon.Core.Services;

public interface IFileUploadService
{
    Task<FileUploadResult> UploadImageAsync(FileUploadRequest file, bool createResizedImages = true, string? uploadSessionIdentifier = null);
}
