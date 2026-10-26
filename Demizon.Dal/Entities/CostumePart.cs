namespace Demizon.Dal.Entities;

/// <summary>Součástka / druh, např. „Kopanická vesta“. Konkrétní kusy jsou <see cref="CostumeItem"/>.</summary>
public class CostumePart
{
    public int Id { get; set; }

    public int? CostumeId { get; set; }

    public virtual Costume? Costume { get; set; }

    public string Name { get; set; } = null!;

    public CostumeGender Gender { get; set; }

    public string? Description { get; set; }

    public CostumeTrackingMode TrackingMode { get; set; }

    public virtual List<CostumeItem> Items { get; set; } = [];
}

public enum CostumeGender
{
    Male,
    Female,
    Unisex
}

public enum CostumeTrackingMode
{
    /// <summary>Každý kus je samostatná položka s vlastním QR, počet je vždy 1.</summary>
    Individual,

    /// <summary>Položka je šarže N zaměnitelných kusů (stuhy, ponožky) s jedním QR.</summary>
    Bulk
}
