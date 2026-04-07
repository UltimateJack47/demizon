namespace Demizon.Dal.Entities;

public class VideoLink
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public string Url { get; set; } = null!;

    public int Year { get; set; }

    public bool IsInternal { get; set; } = false;

    public int? DanceId { get; set; }

    public virtual Dance? Dance { get; set; }
}