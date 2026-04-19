namespace Demizon.Dal.Entities;

public class File
{
    public int Id { get; set; }

    public virtual Member? Member { get; set; }

    public int? MemberId { get; set; }

    public int? DanceId { get; set; }

    public virtual Dance? Dance { get; set; }

    public string Path { get; set; } = null!;

    public string FileExtension { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    public bool IsPublic { get; set; } = false;

    public byte[]? Data { get; set; }

    public byte[]? ThumbnailData { get; set; }
}