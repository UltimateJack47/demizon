using Demizon.Common;
using Demizon.Dal.Entities;

namespace Demizon.Core.Services.Costume;

/// <summary>
/// Katalog krojárky: kroje, součástky, kusy a stav zásob. Zápisy smí jen správce krojárky —
/// to hlídá volající (atribut na API/stránce), služba sama role nezná.
/// </summary>
public interface ICostumeCatalogService
{
    /// <summary>Všechny součástky i s krojem a kusy (včetně vyřazených), seřazené podle kroje.</summary>
    Task<IReadOnlyList<CostumePart>> GetPartsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Dal.Entities.Costume>> GetCostumesAsync(CancellationToken ct = default);

    Task<CostumeItem?> GetItemAsync(int id, CancellationToken ct = default);

    /// <summary>Kód z QR nebo ručně zadaný; velikost písmen ani mezery kolem nevadí.</summary>
    Task<CostumeItem?> GetItemByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Stav zásob; bez <paramref name="itemIds"/> pro všechny položky.</summary>
    Task<IReadOnlyDictionary<int, CostumeItemStock>> GetStockAsync(
        IReadOnlyCollection<int>? itemIds = null, CancellationToken ct = default);

    /// <summary>Kdo co drží; bez <paramref name="memberId"/> všichni.</summary>
    Task<IReadOnlyList<CostumeHolding>> GetHoldingsAsync(int? memberId = null, CancellationToken ct = default);

    Task<IReadOnlyDictionary<int, CostumeMemberName>> GetMemberNamesAsync(
        IEnumerable<int> memberIds, CancellationToken ct = default);

    Task<Result<int>> CreateCostumeAsync(CostumeInput input, CancellationToken ct = default);
    Task<Result> UpdateCostumeAsync(int id, CostumeInput input, CancellationToken ct = default);

    /// <summary>Jen kroj bez součástek; jinak <c>Rejected</c>.</summary>
    Task<Result> DeleteCostumeAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreatePartAsync(CostumePartInput input, CancellationToken ct = default);

    /// <summary>Režim evidence jde změnit jen u součástky bez kusů.</summary>
    Task<Result> UpdatePartAsync(int id, CostumePartInput input, CancellationToken ct = default);

    /// <summary>Jen součástku bez kusů; kusy s historií se vyřazují, nemažou.</summary>
    Task<Result> DeletePartAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateItemAsync(CostumeItemInput input, CancellationToken ct = default);

    /// <summary>Počet šarže nejde snížit pod to, co je rozpůjčené nebo v rozpracované předávce.</summary>
    Task<Result> UpdateItemAsync(int id, CostumeItemInput input, CancellationToken ct = default);

    /// <summary>
    /// Ruční stav: dostupný, ztracený, vyřazený. Do údržby se kus dává přes údržbu.
    /// Vyřadit jde jen kus, který nikdo nedrží a není v otevřené předávce.
    /// </summary>
    Task<Result> SetItemStateAsync(int id, CostumeItemState state, CancellationToken ct = default);
}
