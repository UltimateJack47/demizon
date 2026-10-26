namespace Demizon.Dal.Entities;

public class CostumeMaintenance
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public virtual CostumeItem Item { get; set; } = null!;

    public CostumeMaintenanceKind Kind { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Null = kus je pořád v údržbě.</summary>
    public DateTime? FinishedAt { get; set; }

    public string? Note { get; set; }

    public int? CostCzk { get; set; }

    public int CreatedById { get; set; }
}

public enum CostumeMaintenanceKind
{
    Washing,
    Cleaning,
    Repair,
    Other
}
