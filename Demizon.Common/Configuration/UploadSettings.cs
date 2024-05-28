namespace Demizon.Common.Configuration;

public class UploadSettings
{
    public string ImagesDirectory { get; set; } = "files/images";

    public List<string> AllowedFileExtensions { get; set; } = new();
    
    public Dictionary<string, ResizeSettings> Resize { get; set; } = null!;
}

public class ResizeSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
}
