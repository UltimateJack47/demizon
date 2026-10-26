namespace Demizon.Dal.Entities;

public class CostumeTransferLine
{
    public int Id { get; set; }

    public int TransferId { get; set; }

    public virtual CostumeTransfer Transfer { get; set; } = null!;

    public int ItemId { get; set; }

    public virtual CostumeItem Item { get; set; } = null!;

    public int Quantity { get; set; } = 1;

    public string? ConditionNote { get; set; }

    public virtual List<CostumePhoto> Photos { get; set; } = [];
}
