namespace Demizon.Dal.Entities;

public class Attendance
{
    public int Id { get; set; }

    public bool Attends { get; set; } = false;

    public string? Comment { get; set; }

    public DateTime Date { get; set; }

    public int? EventId { get; set; }

    public virtual Event? Event { get; set; }

    public int MemberId { get; set; }

    public virtual Member Member { get; set; } = null!;
}
