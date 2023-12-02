namespace Demizon.Core.Services;

public interface IFileUploadService
{
    public Task<FileUploadResult> UploadImageAsync(FileUploadRequest file, bool createResizedImages = true,
        string? uploadSessionIdentifier = null);
}
