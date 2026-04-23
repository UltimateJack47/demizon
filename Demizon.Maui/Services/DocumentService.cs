namespace Demizon.Maui.Services;

public interface IDocumentService
{
    Task<bool> DownloadAndOpenAsync(int fileId, string fileName, string contentType);
}

public class DocumentService(IApiClient apiClient) : IDocumentService
{
    public async Task<bool> DownloadAndOpenAsync(int fileId, string fileName, string contentType)
    {
        var safeName = SanitizeFileName(fileName);
        var localPath = Path.Combine(FileSystem.CacheDirectory, safeName);

        try
        {
            using var response = await apiClient.DownloadDocumentAsync(fileId);
            if (!response.IsSuccessStatusCode)
                return false;

            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(localPath))
            {
                await input.CopyToAsync(output);
            }

            await Launcher.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(localPath, contentType),
                Title = fileName
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? $"doc_{Guid.NewGuid():N}" : name;
    }
}
