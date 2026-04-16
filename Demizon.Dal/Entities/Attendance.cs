namespace Demizon.Dal.Entities;

public enum AttendanceStatus
{
    No = 0,
    Yes = 1,
    Maybe = 2
}

public class Attendance
{
    public int Id { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.No;

    public string? Comment { get; set; }

    public AttendanceActivityRole? ActivityRole { get; set; }

    public DateTime Date { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.Now;

    /// <summary>
    /// ID události v Google Calendar vytvořené pro tento záznam docházky. Null = žádná událost.
    /// </summary>
    public string? GoogleEventId { get; set; }

    public int? EventId { get; set; }

    public virtual Event? Event { get; set; }

    public int MemberId { get; set; }

    public virtual Member Member { get; set; } = null!;
}

public enum AttendanceActivityRole
{
    Dancer,
    Musician
}
