namespace Demizon.Core.Services.S3;

public interface IS3Service
{
    string? GetTestImage();
    string GetImage(string id);
    Task<string?> UploadImage(FileUploadRequest file);
}
