namespace Demizon.Dal.Entities;

/// <summary>
/// Evidenční položka: jeden kus (<see cref="CostumeTrackingMode.Individual"/>) nebo šarže
/// <see cref="Quantity"/> stejných kusů (<see cref="CostumeTrackingMode.Bulk"/>). V krojárce je
/// <c>Quantity − Σ Holdings.Quantity</c>; krojárka sama řádek v <see cref="CostumeHolding"/> nemá.
/// </summary>
public class CostumeItem
{
    public int Id { get; set; }

    public int PartId { get; set; }

    public virtual CostumePart Part { get; set; } = null!;

    /// <summary>Inventární kód, nese ho QR štítek. Unikátní.</summary>
    public string Code { get; set; } = null!;

    /// <summary>Volný text podle značení krojařky (sukně „1“–„6“, nohavice „106“).</summary>
    public string? Size { get; set; }

    public string? Color { get; set; }

    public string? Note { get; set; }

    /// <summary>Hodnota v celých Kč — SQLite <c>decimal</c> v EF neumí řadit ani sčítat v DB.</summary>
    public int? ValueCzk { get; set; }

    public int Quantity { get; set; } = 1;

    public CostumeItemState State { get; set; } = CostumeItemState.Available;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual List<CostumeHolding> Holdings { get; set; } = [];
}

public enum CostumeItemState
{
    Available,
    InMaintenance,
    Lost,

    /// <summary>Vyřazený kus se nemaže, aby zůstala jeho historie předávek.</summary>
    Retired
}
