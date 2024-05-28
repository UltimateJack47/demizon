namespace Demizon.Core.Services.S3;

public class ResultListRaw
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? FileId { get; set; }
    public object? Tags { get; set; }
    public object? AiTags { get; set; }
    public VersionInfo? VersionInfo { get; set; }
    public EmbeddedMetadata? EmbeddedMetadata { get; set; }
    public object? CustomCoordinates { get; set; }
    public CustomMetadata? CustomMetadata { get; set; }
    public bool IsPrivateFile { get; set; }
    public string? Url { get; set; }
    public string? Thumbnail { get; set; }
    public string? FileType { get; set; }
    public string? FilePath { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public int Size { get; set; }
    public bool HasAlpha { get; set; }
    public string? Mime { get; set; }
}

public class VersionInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class EmbeddedMetadata
{
    public string? DateCreated { get; set; }
    public string? DateTimeCreated { get; set; }
}

public class CustomMetadata
{
}
