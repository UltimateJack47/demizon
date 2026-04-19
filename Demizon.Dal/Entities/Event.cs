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

    public bool IsCancelled { get; set; } = false;

    /// <summary>
    /// Kolik dní předem odeslat push notifikaci členům. Null = notifikace se neodesílá.
    /// </summary>
    public int? NotifyBeforeDays { get; set; }

    public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;

    public DateTime? RecurrenceEndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual List<Attendance> Attendances { get; set; } = [];
}

public enum RecurrenceType
{
    None = 0,
    Weekly = 1,
    Monthly = 2,
}
