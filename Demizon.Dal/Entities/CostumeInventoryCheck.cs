namespace Demizon.Dal.Entities;

/// <summary>Inventura: co správce v krojárce naskenoval. Srovnání s očekávaným stavem počítá služba.</summary>
public class CostumeInventoryCheck
{
    public int Id { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FinishedAt { get; set; }

    public int StartedById { get; set; }

    public virtual List<CostumeInventoryEntry> Entries { get; set; } = [];
}

public class CostumeInventoryEntry
{
    public int Id { get; set; }

    public int CheckId { get; set; }

    public virtual CostumeInventoryCheck Check { get; set; } = null!;

    public int ItemId { get; set; }

    public virtual CostumeItem Item { get; set; } = null!;

    /// <summary>Spočtený počet; u kusu 1.</summary>
    public int CountedQuantity { get; set; } = 1;

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}
