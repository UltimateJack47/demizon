namespace Demizon.Core.Services.FileUpload;

public class FileUploadRequest
{
    public required Stream Stream { get; set; }
    public string FileName { get; set; } = null!;
    public string FileExtension { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
}