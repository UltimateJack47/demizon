namespace Demizon.Dal.Entities;

public class DanceNumber
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Lyrics { get; set; }

    public int DanceId { get; set; }

    public virtual Dance Dance { get; set; } = null!;
}
