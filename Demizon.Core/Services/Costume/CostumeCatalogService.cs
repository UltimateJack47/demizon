using Demizon.Common;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Costume;

public sealed class CostumeCatalogService(DemizonContext db, ILogger<CostumeCatalogService> logger)
    : ICostumeCatalogService
{
    /// <summary>Předávky, které drží kusy zamčené: rozpracované a čekající na potvrzení.</summary>
    internal static readonly CostumeTransferStatus[] OpenStatuses =
        [CostumeTransferStatus.Draft, CostumeTransferStatus.Pending];

    public async Task<IReadOnlyList<CostumePart>> GetPartsAsync(CancellationToken ct = default) =>
        await db.CostumeParts
            .Include(p => p.Costume)
            .Include(p => p.Items)
            .OrderBy(p => p.Costume == null)
            .ThenBy(p => p.Costume!.SortOrder)
            .ThenBy(p => p.Costume!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Dal.Entities.Costume>> GetCostumesAsync(CancellationToken ct = default) =>
        await db.Costumes.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<CostumeItem?> GetItemAsync(int id, CancellationToken ct = default) =>
        db.CostumeItems
            .Include(i => i.Part).ThenInclude(p => p.Costume)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<CostumeItem?> GetItemByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = CostumeCodes.Normalize(code);
        return db.CostumeItems
            .Include(i => i.Part).ThenInclude(p => p.Costume)
            .FirstOrDefaultAsync(i => i.Code == normalized, ct);
    }

    public async Task<IReadOnlyDictionary<int, CostumeItemStock>> GetStockAsync(
        IReadOnlyCollection<int>? itemIds = null, CancellationToken ct = default)
    {
        var items = db.CostumeItems.AsQueryable();
        var holdings = db.CostumeHoldings.AsQueryable();
        var reservedLines = db.CostumeTransferLines
            .Where(l => l.Transfer.FromMemberId == null && OpenStatuses.Contains(l.Transfer.Status));
        if (itemIds is not null)
        {
            items = items.Where(i => itemIds.Contains(i.Id));
            holdings = holdings.Where(h => itemIds.Contains(h.ItemId));
            reservedLines = reservedLines.Where(l => itemIds.Contains(l.ItemId));
        }

        var quantities = await items.Select(i => new { i.Id, i.Quantity }).ToListAsync(ct);
        var held = await holdings.GroupBy(h => h.ItemId)
            .Select(g => new { ItemId = g.Key, Sum = g.Sum(h => h.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Sum, ct);
        var reserved = await reservedLines.GroupBy(l => l.ItemId)
            .Select(g => new { ItemId = g.Key, Sum = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Sum, ct);

        return quantities.ToDictionary(
            i => i.Id,
            i => new CostumeItemStock(i.Id, i.Quantity, held.GetValueOrDefault(i.Id), reserved.GetValueOrDefault(i.Id)));
    }

    public async Task<IReadOnlyList<CostumeHolding>> GetHoldingsAsync(int? memberId = null, CancellationToken ct = default)
    {
        var query = db.CostumeHoldings
            .Include(h => h.Item).ThenInclude(i => i.Part).ThenInclude(p => p.Costume)
            .AsQueryable();
        if (memberId is not null)
            query = query.Where(h => h.MemberId == memberId);
        return await query.OrderBy(h => h.MemberId).ThenBy(h => h.Item.Code).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, CostumeMemberName>> GetMemberNamesAsync(
        IEnumerable<int> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        // Záměrně i soft-smazaní: bývalý člen, který drží kroj, se musí v přehledu ukázat jménem.
        return await db.Members.IgnoreQueryFilters()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new CostumeMemberName(m.Id, m.Name + " " + m.Surname, m.DeletedAt != null))
            .ToDictionaryAsync(m => m.Id, ct);
    }

    // ------------------------------------------------------------------ kroje

    public async Task<Result<int>> CreateCostumeAsync(CostumeInput input, CancellationToken ct = default)
    {
        if (ValidateCostume(input) is { } error)
            return Result<int>.Rejected(error);

        try
        {
            var costume = new Dal.Entities.Costume();
            Apply(costume, input);
            db.Costumes.Add(costume);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(costume.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to create costume {Name}.", input.Name);
            return Result<int>.Fail("Kroj se nepodařilo uložit.");
        }
    }

    public async Task<Result> UpdateCostumeAsync(int id, CostumeInput input, CancellationToken ct = default)
    {
        if (ValidateCostume(input) is { } error)
            return Result.Rejected(error);

        try
        {
            var costume = await db.Costumes.FindAsync([id], ct);
            if (costume is null)
                return Result.NotFound("Kroj nebyl nalezen.");

            Apply(costume, input);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to update costume {Id}.", id);
            return Result.Fail("Kroj se nepodařilo uložit.");
        }
    }

    public async Task<Result> DeleteCostumeAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var costume = await db.Costumes.FindAsync([id], ct);
            if (costume is null)
                return Result.NotFound("Kroj nebyl nalezen.");
            if (await db.CostumeParts.AnyAsync(p => p.CostumeId == id, ct))
                return Result.Rejected("Kroj má součástky. Nejdřív je přesuň nebo smaž.");

            db.Costumes.Remove(costume);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to delete costume {Id}.", id);
            return Result.Fail("Kroj se nepodařilo smazat.");
        }
    }

    private static string? ValidateCostume(CostumeInput input) =>
        ValidateName(input.Name) ?? ValidateText(input.Description, "Popis");

    private static void Apply(Dal.Entities.Costume costume, CostumeInput input)
    {
        costume.Name = input.Name.Trim();
        costume.Description = Clean(input.Description);
        costume.SortOrder = input.SortOrder;
    }

    // ------------------------------------------------------------- součástky

    public async Task<Result<int>> CreatePartAsync(CostumePartInput input, CancellationToken ct = default)
    {
        try
        {
            if (await ValidatePartAsync(input, ct) is { } error)
                return Result<int>.Rejected(error);

            var part = new CostumePart();
            Apply(part, input);
            db.CostumeParts.Add(part);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(part.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to create costume part {Name}.", input.Name);
            return Result<int>.Fail("Součástku se nepodařilo uložit.");
        }
    }

    public async Task<Result> UpdatePartAsync(int id, CostumePartInput input, CancellationToken ct = default)
    {
        try
        {
            var part = await db.CostumeParts.FindAsync([id], ct);
            if (part is null)
                return Result.NotFound("Součástka nebyla nalezena.");
            if (await ValidatePartAsync(input, ct) is { } error)
                return Result.Rejected(error);
            if (part.TrackingMode != input.TrackingMode && await db.CostumeItems.AnyAsync(i => i.PartId == id, ct))
                return Result.Rejected("Režim evidence jde změnit jen u součástky bez kusů.");

            Apply(part, input);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to update costume part {Id}.", id);
            return Result.Fail("Součástku se nepodařilo uložit.");
        }
    }

    public async Task<Result> DeletePartAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var part = await db.CostumeParts.FindAsync([id], ct);
            if (part is null)
                return Result.NotFound("Součástka nebyla nalezena.");
            if (await db.CostumeItems.AnyAsync(i => i.PartId == id, ct))
                return Result.Rejected("Součástka má kusy. Kusy s historií se vyřazují, ne mažou.");

            db.CostumeParts.Remove(part);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to delete costume part {Id}.", id);
            return Result.Fail("Součástku se nepodařilo smazat.");
        }
    }

    private async Task<string?> ValidatePartAsync(CostumePartInput input, CancellationToken ct)
    {
        if ((ValidateName(input.Name) ?? ValidateText(input.Description, "Popis")) is { } error)
            return error;
        if (input.CostumeId is { } costumeId && !await db.Costumes.AnyAsync(c => c.Id == costumeId, ct))
            return "Kroj nebyl nalezen.";
        return null;
    }

    private static void Apply(CostumePart part, CostumePartInput input)
    {
        part.CostumeId = input.CostumeId;
        part.Name = input.Name.Trim();
        part.Gender = input.Gender;
        part.Description = Clean(input.Description);
        part.TrackingMode = input.TrackingMode;
    }

    // ------------------------------------------------------------------ kusy

    public async Task<Result<int>> CreateItemAsync(CostumeItemInput input, CancellationToken ct = default)
    {
        try
        {
            var part = await db.CostumeParts.FindAsync([input.PartId], ct);
            if (part is null)
                return Result<int>.Rejected("Součástka nebyla nalezena.");

            var code = string.IsNullOrWhiteSpace(input.Code)
                ? CostumeCodes.Next(await db.CostumeItems
                    .Where(i => i.Code.StartsWith(CostumeCodes.Prefix))
                    .Select(i => i.Code)
                    .ToListAsync(ct))
                : CostumeCodes.Normalize(input.Code);
            if (await ValidateItemAsync(input, part, code, itemId: null, ct) is { } error)
                return Result<int>.Rejected(error);

            var item = new CostumeItem { Code = code };
            Apply(item, input, part);
            db.CostumeItems.Add(item);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(item.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to create costume item for part {PartId}.", input.PartId);
            return Result<int>.Fail("Kus se nepodařilo uložit.");
        }
    }

    public async Task<Result> UpdateItemAsync(int id, CostumeItemInput input, CancellationToken ct = default)
    {
        try
        {
            var item = await db.CostumeItems.FindAsync([id], ct);
            if (item is null)
                return Result.NotFound("Kus nebyl nalezen.");
            var part = await db.CostumeParts.FindAsync([input.PartId], ct);
            if (part is null)
                return Result.Rejected("Součástka nebyla nalezena.");

            var code = string.IsNullOrWhiteSpace(input.Code) ? item.Code : CostumeCodes.Normalize(input.Code);
            if (await ValidateItemAsync(input, part, code, id, ct) is { } error)
                return Result.Rejected(error);

            var stock = (await GetStockAsync([id], ct))[id];
            var quantity = part.TrackingMode == CostumeTrackingMode.Individual ? 1 : input.Quantity;
            if (quantity < stock.Held + stock.Reserved)
                return Result.Rejected(
                    $"Počet nejde snížit pod {stock.Held + stock.Reserved} — tolik je rozpůjčeno nebo v předávce.");

            item.Code = code;
            Apply(item, input, part);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to update costume item {Id}.", id);
            return Result.Fail("Kus se nepodařilo uložit.");
        }
    }

    public async Task<Result> SetItemStateAsync(int id, CostumeItemState state, CancellationToken ct = default)
    {
        if (state == CostumeItemState.InMaintenance)
            return Result.Rejected("Do údržby se kus dává přes záznam údržby.");

        try
        {
            var item = await db.CostumeItems.FindAsync([id], ct);
            if (item is null)
                return Result.NotFound("Kus nebyl nalezen.");
            if (item.State == CostumeItemState.InMaintenance)
                return Result.Rejected("Kus je v údržbě. Nejdřív údržbu ukonči.");

            if (state == CostumeItemState.Retired)
            {
                if (await db.CostumeHoldings.AnyAsync(h => h.ItemId == id, ct))
                    return Result.Rejected("Kus má půjčený člen. Vyřadit jde až po vrácení.");
                if (await IsInOpenTransferAsync(id, ct))
                    return Result.Rejected("Kus je v rozpracované předávce.");
            }

            item.State = state;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to set state of costume item {Id}.", id);
            return Result.Fail("Stav kusu se nepodařilo uložit.");
        }
    }

    private Task<bool> IsInOpenTransferAsync(int itemId, CancellationToken ct) =>
        db.CostumeTransferLines.AnyAsync(l => l.ItemId == itemId && OpenStatuses.Contains(l.Transfer.Status), ct);

    private async Task<string?> ValidateItemAsync(
        CostumeItemInput input, CostumePart part, string code, int? itemId, CancellationToken ct)
    {
        if (CostumeCodes.Validate(code) is { } codeError)
            return codeError;
        if (await db.CostumeItems.AnyAsync(i => i.Code == code && i.Id != itemId, ct))
            return $"Kód {code} už má jiný kus.";
        if (part.TrackingMode == CostumeTrackingMode.Bulk && input.Quantity < 1)
            return "Počet musí být aspoň 1.";
        if (input.ValueCzk < 0)
            return "Hodnota nesmí být záporná.";
        return ValidateShort(input.Size, "Velikost") ?? ValidateShort(input.Color, "Barva")
            ?? ValidateText(input.Note, "Poznámka");
    }

    private static void Apply(CostumeItem item, CostumeItemInput input, CostumePart part)
    {
        item.PartId = part.Id;
        item.Size = Clean(input.Size);
        item.Color = Clean(input.Color);
        item.Note = Clean(input.Note);
        item.ValueCzk = input.ValueCzk;
        item.Quantity = part.TrackingMode == CostumeTrackingMode.Individual ? 1 : input.Quantity;
    }

    // ------------------------------------------------------------- validace

    private static string? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Název je povinný."
        : name.Trim().Length > DemizonContext.CostumeNameMaxLength
            ? $"Název může mít nejvýš {DemizonContext.CostumeNameMaxLength} znaků."
        : null;

    private static string? ValidateText(string? text, string label) =>
        text?.Trim().Length > DemizonContext.CostumeTextMaxLength
            ? $"{label} může mít nejvýš {DemizonContext.CostumeTextMaxLength} znaků."
            : null;

    private static string? ValidateShort(string? text, string label) =>
        text?.Trim().Length > 50 ? $"{label} může mít nejvýš 50 znaků." : null;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
