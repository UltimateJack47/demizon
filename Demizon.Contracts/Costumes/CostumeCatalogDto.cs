namespace Demizon.Contracts.Costumes;

public sealed record CostumeDto(int Id, string Name, string? Description, int SortOrder);

/// <summary>Kus se stavem zásob. <c>InStore</c> = v krojárce, <c>Reserved</c> = z toho v otevřených předávkách.</summary>
public sealed record CostumeStockItemDto(CostumeItemDto Item, int InStore, int Held, int Reserved);

public sealed record CostumePartDto(
    int Id,
    int? CostumeId,
    string Name,
    string Gender,
    string? Description,
    string TrackingMode,
    List<CostumeStockItemDto> Items);

public sealed record CostumeCatalogDto(List<CostumeDto> Costumes, List<CostumePartDto> Parts);

public sealed record CostumeHolderOverviewDto(CostumePartyDto Member, List<CostumeHoldingDto> Holdings);
