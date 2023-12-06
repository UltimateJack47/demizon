using Imagekit.Models;
using Imagekit.Sdk;

namespace Demizon.Core.Services.S3;

public class S3Service : IS3Service
{
    public S3Service(ImagekitClient imagekit)
    {
        Imagekit = imagekit;
    }

    private ImagekitClient Imagekit { get; set; }

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
}
