namespace DomProject.Dal.Entities;

public class Borrowing
{
    public int Id { get; set; }
    
    public int DeviceId { get; set; }

    public virtual Device Device { get; set; } = null!;

    public int UserId { get; set; }

    public virtual User User { get; set; } = null!;

    public DateTime Start { get; set; }

    public DateTime? End { get; set; }
}
