using System.Runtime.InteropServices.ComTypes;

namespace Demizon.Core.Services;

public class FileUploadRequest
{
    public IStream Stream { get; set; }
    public string FileName { get; set; } = null!;
    public string FileExtension { get; set; } = null!;
}
