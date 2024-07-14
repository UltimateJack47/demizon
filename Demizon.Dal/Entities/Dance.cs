namespace Demizon.Dal.Entities;

public class Dance
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public string? Lyrics { get; set; }

    public virtual List<File> Files { get; set; } = [];

    public virtual List<VideoLink> Videos { get; set; } = [];
}