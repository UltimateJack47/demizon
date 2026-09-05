namespace Demizon.Common.Configuration;

public class UploadSettings
{
    public string ImagesDirectory { get; set; } = "files/images";

    public List<string> AllowedFileExtensions { get; set; } = new();

    public Dictionary<string, ResizeSettings> Resize { get; set; } = null!;

    /// <summary>Max size of a single uploaded file (default 25 MB).</summary>
    public long MaxFileBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>
    /// Max total <c>Files.FileSize</c> sum stored in SQLite BLOBs.
    /// Default 2 GB leaves headroom on a 10 GB Stardust disk for OS, image, WAL, and growth.
    /// </summary>
    public long MaxTotalStorageBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>Max number of rows in the Files table (default 2 000).</summary>
    public int MaxFileCount { get; set; } = 2_000;
}

public class ResizeSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
}
