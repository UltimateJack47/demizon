using System.Collections.ObjectModel;
using System.Runtime.InteropServices.ComTypes;

namespace Demizon.Core.Services;

public interface IFileUploadService
{
    Task<Collection<FileUploadResult>> UploadImageAsync(FileUploadRequest file, bool createResizedImages = true, string? uploadSessionIdentifier = null);
}
