namespace Demizon.Dal.Entities;

/// <summary>
/// Kolik kusů položky drží člen. Mění se jen potvrzením předávky.
/// </summary>
/// <remarks>
/// Na člena vede jen cizí klíč, ne navigace: <c>Member</c> má soft-delete filtr a povinná
/// navigace by v dotazu s <c>Include</c> řádky smazaného člena tiše zahodila — právě ty,
/// které krojárka potřebuje vidět nejvíc (bývalý člen pořád drží kroj).
/// </remarks>
public class CostumeHolding
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public virtual CostumeItem Item { get; set; } = null!;

    public int MemberId { get; set; }

    public int Quantity { get; set; }
}
