namespace Demizon.Dal.Entities;

/// <summary>Kroj, např. „Kopanický“. Seskupuje součástky; součástka bez kroje je „ostatní“.</summary>
public class Costume
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public virtual List<CostumePart> Parts { get; set; } = [];
}
