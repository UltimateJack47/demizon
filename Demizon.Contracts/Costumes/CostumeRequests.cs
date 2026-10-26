namespace Demizon.Contracts.Costumes;

public sealed record CostumeLineRequest(string Code, int Quantity = 1);

/// <summary>
/// <c>FromMemberId</c> null = z krojárky. Příjemce buď <c>ToMemberId</c>, nebo <c>ToQrToken</c>
/// z naskenovaného profilu; bez obojího = vrácení do krojárky.
/// </summary>
public sealed record CreateCostumeTransferRequest(
    int? FromMemberId,
    int? ToMemberId,
    string? ToQrToken,
    List<CostumeLineRequest> Lines,
    string? Note);

public sealed record CostumeLineNoteRequest(string? ConditionNote);

public sealed record RejectCostumeTransferRequest(string Reason);

public sealed record CostumeRequest(string Name, string? Description, int SortOrder);

public sealed record CostumePartRequest(
    int? CostumeId,
    string Name,
    string Gender,
    string? Description,
    string TrackingMode);

public sealed record CostumeItemRequest(
    int PartId,
    string? Code,
    string? Size,
    string? Color,
    string? Note,
    int? ValueCzk,
    int Quantity = 1);

public sealed record CostumeItemStateRequest(string State);

/// <summary>Člen podle QR z profilu — aby UI ukázalo, komu se předává, dřív než se předávka založí.</summary>
public sealed record CostumeMemberDto(int Id, string FullName);
