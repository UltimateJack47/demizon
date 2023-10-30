namespace Demizon.Core.Services;

public class FileUploadResult
{
    public bool IsSuccessful { get; set; }

    public string FileName { get; set; } = null!;
    
    public string FileExtension { get; set; } = null!;
    
    public string RelativePath { get; set; } = null!;
    
    public long FileSize { get; set; }
    
    public string ContentType { get; set; } = null!;
}
