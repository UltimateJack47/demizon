using Demizon.Common.Configuration;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.FileUpload;

public class FileUploadService(IOptionsSnapshot<UploadSettings> uploadSettings) : IFileUploadService
{
    private UploadSettings UploadSettings { get; } = uploadSettings.Value;

    public async Task<FileUploadResult> UploadImageAsync(FileUploadRequest fileRequest,
        bool createResizedImages = false, string? uploadSessionIdentifier = null)
    {
        string documentRoot = Environment.CurrentDirectory;

        // Create directory
        uploadSessionIdentifier ??= Guid.NewGuid().ToString();
        string fileRelPathDir = $"{UploadSettings.ImagesDirectory}/{uploadSessionIdentifier}/";
        Directory.CreateDirectory(documentRoot + "/" + fileRelPathDir);

        // Set paths
        string fileName = Guid.NewGuid() + fileRequest.FileExtension;
        Uri fileUri = new Uri(documentRoot + "/" + fileRelPathDir + fileName);

        await using (var stream = new FileStream(fileUri.AbsolutePath, FileMode.Create))
        {
            await fileRequest.Stream.CopyToAsync(stream);
        }


        /*if (createResizedImages)
        {
            ResizeAndCreate(fileUri, fileName);
        }*/

        return new FileUploadResult
        {
            FileExtension = fileRequest.FileExtension,
            FileName = Path.GetFileNameWithoutExtension(fileName),
            RelativePath = fileRelPathDir + fileName,
            ContentType = fileRequest.ContentType,
            FileSize = fileRequest.FileSize,
            IsSuccessful = true
        };
    }


    private void ResizeAndCreate(Uri fileUri, string fileName)
    {
        foreach (var (resizeName, resizeDimensions) in UploadSettings.Resize)
        {
            using var image = new MagickImage(fileUri.AbsolutePath);
            if (image.Height <= resizeDimensions.Height && image.Width <= resizeDimensions.Width)
            {
                continue;
            }

            image.Resize(resizeDimensions.Width, resizeDimensions.Height);
            string resizedFileName = resizeName.ToLower() + "_" + fileName;
            string resizedFile = Path.GetDirectoryName(fileUri.AbsolutePath) + "/" + resizedFileName;
            image.Write(resizedFile);
        }
    }
}