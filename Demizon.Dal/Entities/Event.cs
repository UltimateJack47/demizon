namespace Demizon.Dal.Entities;

public class Event
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime DateFrom { get; set; }

    public DateTime DateTo { get; set; }

    public string? Place { get; set; }

    public string? Information { get; set; }

    public bool IsPublic { get; set; } = false;

    public virtual List<Attendance> Attendances { get; set; } = [];
}
