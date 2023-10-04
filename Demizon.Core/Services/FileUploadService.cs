using System.Collections.ObjectModel;
using Demizon.Common.Configuration;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services;

public class FileUploadService : IFileUploadService
{
    private UploadSettings UploadSettings { get; }

    private FileUploadService(IOptionsSnapshot<UploadSettings> uploadSettings)
    {
        UploadSettings = uploadSettings.Value;
    }

    public async Task<Collection<FileUploadResult>> UploadImageAsync(FileUploadRequest fileRequest,
        bool createResizedImages = true, string? uploadSessionIdentifier = null)
    {
        string documentRoot = Environment.CurrentDirectory;
        Collection<FileUploadResult> imgUpResults = new Collection<FileUploadResult>();

        // Create directory
        uploadSessionIdentifier ??= new Guid().ToString();
        string fileRelPathDir = $"{UploadSettings.ImagesDirectory}/{uploadSessionIdentifier}/";
        Directory.CreateDirectory(documentRoot + "/" + fileRelPathDir);

        // Set paths
        string fileExtension = Path.GetExtension(fileRequest.FileName).ToLower()!;
        string fileName = Guid.NewGuid() + fileExtension;
        Uri fileUri = new Uri(documentRoot + "/" + fileRelPathDir + fileName);

        await using (var stream = new FileStream(fileUri.AbsolutePath, FileMode.Create))
        {
            //fileRequest.Stream.CopyTo(stream);
        }


        if (createResizedImages)
        {
            ResizeAndCreate(fileUri, fileName);
        }


        imgUpResults.Add(new FileUploadResult
        {
            FileExtension = fileExtension,
            FileName = Path.GetFileNameWithoutExtension(fileName),
            RelativePath = fileRelPathDir + fileName
        });

        return imgUpResults;
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
