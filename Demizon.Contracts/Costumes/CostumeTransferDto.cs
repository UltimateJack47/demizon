namespace Demizon.Contracts.Costumes;

/// <summary>
/// Předávka. <c>From</c> / <c>To</c> null = krojárka. <c>Status</c>: <c>Draft</c>, <c>Pending</c>,
/// <c>Confirmed</c>, <c>Rejected</c>, <c>Cancelled</c>. Časy jsou UTC.
/// </summary>
public sealed record CostumeTransferDto(
    int Id,
    string Status,
    CostumePartyDto? From,
    CostumePartyDto? To,
    CostumePartyDto CreatedBy,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? ResolvedAt,
    CostumePartyDto? ResolvedBy,
    string? Note,
    string? RejectionReason,
    List<CostumeTransferLineDto> Lines);

public sealed record CostumeTransferLineDto(
    int Id,
    CostumeItemDto Item,
    int Quantity,
    string? ConditionNote,
    List<CostumePhotoDto> Photos);

/// <summary>
/// Fotka bez dat; obrázek je na <c>/api/costumes/transfers/{id}/photos/{photoId}?size=full|thumb</c>.
/// <c>IsStale</c>: podle EXIF pořízená výrazně dřív, než byla nahrána — příjemce má zbystřit.
/// </summary>
public sealed record CostumePhotoDto(int Id, DateTime UploadedAt, DateTime? CapturedAt, bool IsStale);

/// <summary>Co člen drží a které předávky na něj čekají nebo které rozpracoval.</summary>
public sealed record MyCostumesDto(
    List<CostumeHoldingDto> Holdings,
    List<CostumeTransferDto> Incoming,
    List<CostumeTransferDto> Outgoing);

public sealed record CostumeTransferStatusDto(int Id, string Status);
