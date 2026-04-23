namespace Demizon.Core.Services.FileUpload;

public interface IFileUploadService
{
    public Task<FileUploadResult> UploadImageAsync(FileUploadRequest file, bool createResizedImages = true,
        string? uploadSessionIdentifier = null);

    public Task<FileUploadResult> UploadImageToDbAsync(FileUploadRequest file);

    public Task<FileUploadResult> UploadDocumentToDbAsync(FileUploadRequest file);
}