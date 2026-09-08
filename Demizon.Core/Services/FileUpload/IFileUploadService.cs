namespace Demizon.Core.Services.FileUpload;

public interface IFileUploadService
{
    public Task<FileUploadResult> UploadImageToDbAsync(FileUploadRequest file);

    public Task<FileUploadResult> UploadDocumentToDbAsync(FileUploadRequest file);
}