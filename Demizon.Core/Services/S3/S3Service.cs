using Imagekit.Models;
using Imagekit.Sdk;

namespace Demizon.Core.Services.S3;

public class S3Service(ImagekitClient imagekit) : IS3Service
{
    private ImagekitClient Imagekit { get; set; } = imagekit;

    public string GetTestImage()
    {
        GetFileListRequest model = new GetFileListRequest
        {
            Limit = 10,
            Skip = 0
        };        
        
        ResultList res = Imagekit.GetFileListRequest(model);
        return res.FileList.First().url;
    }

    public string GetImage(string id)
    {
        throw new NotImplementedException();
    }

    public string UploadImage(FileUploadRequest file)
    {
        throw new NotImplementedException();
    }
}
