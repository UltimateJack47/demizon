using Demizon.Common.Configuration;
using ImageMagick;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.FileUpload;

public class FileUploadService(IOptionsSnapshot<UploadSettings> uploadSettings) : IFileUploadService
{
    private const int MaxImageWidth = 1200;
    private const int ThumbnailWidth = 200;
    private const int JpegQuality = 80;

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

    public async Task<FileUploadResult> UploadImageToDbAsync(FileUploadRequest fileRequest)
    {
        using var ms = new MemoryStream();
        await fileRequest.Stream.CopyToAsync(ms);
        var originalBytes = ms.ToArray();

        // Optimize full image: max 1200px width, JPEG quality 80%
        byte[] fullData = OptimizeImage(originalBytes, MaxImageWidth, JpegQuality);

        // Create thumbnail: max 200px width
        byte[] thumbData = OptimizeImage(originalBytes, ThumbnailWidth, JpegQuality);

        return new FileUploadResult
        {
            FileExtension = ".jpg",
            FileName = Guid.NewGuid().ToString(),
            RelativePath = "db-stored",
            ContentType = "image/jpeg",
            FileSize = fullData.Length,
            Data = fullData,
            ThumbnailData = thumbData,
            IsSuccessful = true
        };
    }

    private static byte[] OptimizeImage(byte[] imageBytes, int maxWidth, int quality)
    {
        using var image = new MagickImage(imageBytes);

        if (image.Width > maxWidth)
        {
            var ratio = (double)maxWidth / image.Width;
            var newHeight = (uint)(image.Height * ratio);
            image.Resize((uint)maxWidth, newHeight);
        }

        image.Format = MagickFormat.Jpeg;
        image.Quality = (uint)quality;
        image.Strip();

        using var output = new MemoryStream();
        image.Write(output);
        return output.ToArray();
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

            image.Resize((uint)resizeDimensions.Width, (uint)resizeDimensions.Height);
            string resizedFileName = resizeName.ToLower() + "_" + fileName;
            string resizedFile = Path.GetDirectoryName(fileUri.AbsolutePath) + "/" + resizedFileName;
            image.Write(resizedFile);
        }
    }
}