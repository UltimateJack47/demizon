namespace Demizon.Dal.Entities;

/// <summary>
/// Jedna předávka kusů. <c>null</c> u <see cref="FromMemberId"/> / <see cref="ToMemberId"/> znamená
/// krojárku. Držení (<see cref="CostumeHolding"/>) se mění až při <see cref="CostumeTransferStatus.Confirmed"/>;
/// potvrzuje vždy příjemce.
/// </summary>
/// <remarks>Na členy jen cizí klíče, bez navigací — viz <see cref="CostumeHolding"/>.</remarks>
public class CostumeTransfer
{
    public int Id { get; set; }

    public int? FromMemberId { get; set; }

    public int? ToMemberId { get; set; }

    public int CreatedById { get; set; }

    public CostumeTransferStatus Status { get; set; } = CostumeTransferStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int? ResolvedById { get; set; }

    public string? Note { get; set; }

    public string? RejectionReason { get; set; }

    public virtual List<CostumeTransferLine> Lines { get; set; } = [];
}

public enum CostumeTransferStatus
{
    /// <summary>Rozpracovaná, fotí se. Kusy jsou zamčené pro jiné předávky.</summary>
    Draft,

    /// <summary>Odeslaná, čeká na potvrzení příjemcem.</summary>
    Pending,
    Confirmed,
    Rejected,
    Cancelled
}
