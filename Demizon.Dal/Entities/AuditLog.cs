namespace Demizon.Dal.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public string EntityType { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    /// <summary>
    /// Added / Modified / Deleted
    /// </summary>
    public string Action { get; set; } = null!;

    public string UserId { get; set; } = "system";

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON serializace původních hodnot (pouze pro Modified).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON serializace nových hodnot (Added / Modified).
    /// </summary>
    public string? NewValues { get; set; }
}
