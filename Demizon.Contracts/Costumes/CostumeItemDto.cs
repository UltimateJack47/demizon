namespace Demizon.Contracts.Costumes;

/// <summary>
/// Kus nebo šarže. Enumy jako text (<c>Male</c>/<c>Female</c>/<c>Unisex</c>,
/// <c>Individual</c>/<c>Bulk</c>, <c>Available</c>/<c>InMaintenance</c>/<c>Lost</c>/<c>Retired</c>).
/// <c>ValueCzk</c> dostane jen ten, kdo vidí celou krojárku.
/// </summary>
public sealed record CostumeItemDto(
    int Id,
    string Code,
    int PartId,
    string PartName,
    string? CostumeName,
    string Gender,
    string TrackingMode,
    string? Size,
    string? Color,
    string? Note,
    int? ValueCzk,
    int Quantity,
    string State);

/// <summary>Člen v krojárce. <c>IsDeleted</c>: bývalý člen, který pořád něco drží nebo držel.</summary>
public sealed record CostumePartyDto(int MemberId, string FullName, bool IsDeleted);

public sealed record CostumeHoldingDto(CostumeItemDto Item, int Quantity);

public sealed record CostumeHolderDto(CostumePartyDto Member, int Quantity);

/// <summary>Karta kusu po naskenování QR.</summary>
public sealed record CostumeItemCardDto(CostumeItemDto Item, int InStore, List<CostumeHolderDto> Holders);
