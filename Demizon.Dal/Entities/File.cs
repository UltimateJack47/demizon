namespace Demizon.Dal.Entities;

public class File
{
    public int Id { get; set; }

    public virtual User? User { get; set; }

    public int? UserId { get; set; }

    public string Path { get; set; } = null!;

    public string FileExtension { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }
}
