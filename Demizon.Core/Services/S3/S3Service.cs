using System.Text.Json;
using Imagekit.Models;
using Imagekit.Sdk;

namespace Demizon.Core.Services.S3;

public class S3Service(ImagekitClient imagekit) : IS3Service
{
    private ImagekitClient Imagekit { get; set; } = imagekit;

    public string? GetTestImage()
    {
        GetFileListRequest model = new GetFileListRequest
        {
            Limit = 10,
            Skip = 0
        };        
        
        ResultList res = Imagekit.GetFileListRequest(model);
        var neco = JsonSerializer.Deserialize<List<ResultListRaw>>(res.Raw, options: new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        return neco?.FirstOrDefault()?.Url;
    }

    public string GetImage(string id)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> UploadImage(FileUploadRequest file)
    {
        var result = await Imagekit.UploadAsync(new FileCreateRequest()
        {
            file = file.Stream,
            fileName = file.FileName,
            useUniqueFileName = true
        });
        if (result.HttpStatusCode != 200)
        {
            return null;
        }
        return result.url;
    }
}
