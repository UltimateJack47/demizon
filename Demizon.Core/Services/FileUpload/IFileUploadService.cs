namespace Demizon.Core.Services.FileUpload;

public interface IFileUploadService
{
    public Task<FileUploadResult> UploadImageAsync(FileUploadRequest file, bool createResizedImages = true,
        string? uploadSessionIdentifier = null);
}