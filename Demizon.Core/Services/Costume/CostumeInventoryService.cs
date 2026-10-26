using Demizon.Common;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Costume;

public enum CostumeInventoryFinding
{
    /// <summary>Spočteno tolik, kolik má v krojárce být.</summary>
    Ok,

    /// <summary>Má být v krojárce, ale chybí (nebo je ho méně).</summary>
    Missing,

    /// <summary>Šarže: spočteno víc, než podle evidence v krojárce je.</summary>
    Surplus,

    /// <summary>Naskenovaný kus je podle evidence u člena — buď vrácen bez předávky, nebo omyl.</summary>
    HeldByMember,

    /// <summary>Naskenovaný kus je vedený jako ztracený nebo vyřazený — našel se?</summary>
    UnexpectedState
}

/// <param name="Expected">Kolik má podle evidence ležet v krojárce.</param>
public sealed record CostumeInventoryRow(CostumeItem Item, int Expected, int Counted, CostumeInventoryFinding Finding);

/// <summary>
/// Inventura: správce naskenuje, co v krojárce fyzicky je, a služba to porovná s evidencí.
/// Kusy v údržbě se nekontrolují — bývají v čistírně. Inventura nic neopravuje, jen ukáže rozdíly.
/// </summary>
public sealed class CostumeInventoryService(
    DemizonContext db,
    ICostumeCatalogService catalog,
    ILogger<CostumeInventoryService> logger)
{
    public Task<CostumeInventoryCheck?> GetOpenAsync(CancellationToken ct = default) =>
        db.CostumeInventoryChecks.Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.FinishedAt == null, ct);

    public async Task<IReadOnlyList<CostumeInventoryCheck>> GetRecentAsync(int count, CancellationToken ct = default) =>
        await db.CostumeInventoryChecks.OrderByDescending(c => c.StartedAt).Take(count).ToListAsync(ct);

    /// <summary>Najednou běží jen jedna inventura — dvě rozpracované by si skeny pletly.</summary>
    public async Task<Result<int>> StartAsync(int byMemberId, CancellationToken ct = default)
    {
        try
        {
            if (await db.CostumeInventoryChecks.AnyAsync(c => c.FinishedAt == null, ct))
                return Result<int>.Rejected("Jedna inventura už běží. Nejdřív ji dokonči.");

            var check = new CostumeInventoryCheck { StartedById = byMemberId, StartedAt = DateTime.UtcNow };
            db.CostumeInventoryChecks.Add(check);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(check.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to start costume inventory check.");
            return Result<int>.Fail("Inventuru se nepodařilo založit.");
        }
    }

    /// <summary>
    /// Zapíše naskenovaný kus. U kusu je počet vždy 1 (opakovaný sken nic nemění), u šarže každý
    /// sken přičte 1, nebo jde počet zadat přímo přes <paramref name="setCount"/>.
    /// </summary>
    public async Task<Result<CostumeItem>> ScanAsync(int checkId, string code, int? setCount = null,
        CancellationToken ct = default)
    {
        if (setCount < 0)
            return Result<CostumeItem>.Rejected("Počet nesmí být záporný.");

        try
        {
            var check = await db.CostumeInventoryChecks.Include(c => c.Entries)
                .FirstOrDefaultAsync(c => c.Id == checkId, ct);
            if (check is null)
                return Result<CostumeItem>.NotFound("Inventura nebyla nalezena.");
            if (check.FinishedAt is not null)
                return Result<CostumeItem>.Rejected("Inventura už je uzavřená.");

            var item = await catalog.GetItemByCodeAsync(code, ct);
            if (item is null)
                return Result<CostumeItem>.Rejected($"Kus {CostumeCodes.Normalize(code)} v evidenci není.");

            var entry = check.Entries.FirstOrDefault(e => e.ItemId == item.Id);
            if (entry is null)
            {
                entry = new CostumeInventoryEntry { ItemId = item.Id, CountedQuantity = 0 };
                check.Entries.Add(entry);
            }
            entry.CountedQuantity = item.Part.TrackingMode == CostumeTrackingMode.Individual
                ? 1
                : setCount ?? entry.CountedQuantity + 1;
            entry.ScannedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result<CostumeItem>.Ok(item);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to record inventory scan of {Code} in check {CheckId}.", code, checkId);
            return Result<CostumeItem>.Fail("Sken se nepodařilo zapsat.");
        }
    }

    public async Task<Result> FinishAsync(int checkId, CancellationToken ct = default)
    {
        try
        {
            var check = await db.CostumeInventoryChecks.FindAsync([checkId], ct);
            if (check is null)
                return Result.NotFound("Inventura nebyla nalezena.");
            if (check.FinishedAt is not null)
                return Result.Rejected("Inventura už je uzavřená.");

            check.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to finish costume inventory check {CheckId}.", checkId);
            return Result.Fail("Inventuru se nepodařilo uzavřít.");
        }
    }

    /// <summary>
    /// Srovnání s evidencí <b>teď</b>, ne v okamžiku skenu — u uzavřené inventury tedy výsledek
    /// zohlední i předávky, které proběhly po ní.
    /// </summary>
    public async Task<IReadOnlyList<CostumeInventoryRow>> GetResultAsync(int checkId, CancellationToken ct = default)
    {
        var counted = await db.CostumeInventoryEntries.Where(e => e.CheckId == checkId)
            .ToDictionaryAsync(e => e.ItemId, e => e.CountedQuantity, ct);
        var stock = await catalog.GetStockAsync(ct: ct);
        var items = (await catalog.GetPartsAsync(ct)).SelectMany(p => p.Items);

        var rows = new List<CostumeInventoryRow>();
        foreach (var item in items)
        {
            var wasCounted = counted.TryGetValue(item.Id, out var count);
            var expected = item.State is CostumeItemState.Available ? stock[item.Id].InStore : 0;

            CostumeInventoryFinding? finding = item.State switch
            {
                CostumeItemState.InMaintenance => null,
                CostumeItemState.Lost or CostumeItemState.Retired => wasCounted ? CostumeInventoryFinding.UnexpectedState : null,
                _ when count > expected && stock[item.Id].Held > 0 && expected == 0 => CostumeInventoryFinding.HeldByMember,
                _ when count > expected => CostumeInventoryFinding.Surplus,
                _ when count < expected => CostumeInventoryFinding.Missing,
                _ when wasCounted => CostumeInventoryFinding.Ok,
                _ => null
            };
            if (finding is { } f)
                rows.Add(new CostumeInventoryRow(item, expected, count, f));
        }

        return rows.OrderBy(r => r.Finding == CostumeInventoryFinding.Ok).ThenBy(r => r.Item.Code).ToList();
    }
}
