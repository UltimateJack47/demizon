using Demizon.Common;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Costume;

/// <summary>
/// Údržba kusů (praní, čištění, oprava). Kus v údržbě má stav <see cref="CostumeItemState.InMaintenance"/>,
/// takže ho nejde vydat. Do údržby jde jen kus, který je celý v krojárce.
/// </summary>
public sealed class CostumeMaintenanceService(DemizonContext db, ILogger<CostumeMaintenanceService> logger)
{
    public async Task<Result<int>> StartAsync(int itemId, CostumeMaintenanceKind kind, string? note, int? costCzk,
        int byMemberId, CancellationToken ct = default)
    {
        if (ValidateText(note, costCzk) is { } error)
            return Result<int>.Rejected(error);

        try
        {
            var item = await db.CostumeItems.FindAsync([itemId], ct);
            if (item is null)
                return Result<int>.NotFound("Kus nebyl nalezen.");
            if (item.State != CostumeItemState.Available)
                return Result<int>.Rejected($"Do údržby jde jen dostupný kus; {item.Code} je teď jinak.");
            if (await db.CostumeHoldings.AnyAsync(h => h.ItemId == itemId, ct))
                return Result<int>.Rejected($"{item.Code} má někdo půjčený. Do údržby až po vrácení.");
            if (await db.CostumeTransferLines.AnyAsync(l => l.ItemId == itemId
                    && CostumeCatalogService.OpenStatuses.Contains(l.Transfer.Status), ct))
                return Result<int>.Rejected($"{item.Code} je v rozpracované předávce.");

            var record = new CostumeMaintenance
            {
                ItemId = itemId,
                Kind = kind,
                StartedAt = DateTime.UtcNow,
                Note = Clean(note),
                CostCzk = costCzk,
                CreatedById = byMemberId,
            };
            item.State = CostumeItemState.InMaintenance;
            db.CostumeMaintenances.Add(record);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(record.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to start maintenance of costume item {ItemId}.", itemId);
            return Result<int>.Fail("Údržbu se nepodařilo zapsat.");
        }
    }

    /// <summary>Ukončí údržbu a vrátí kus mezi dostupné. Poznámku a cenu jde doplnit až teď.</summary>
    public async Task<Result> FinishAsync(int maintenanceId, string? note, int? costCzk, CancellationToken ct = default)
    {
        if (ValidateText(note, costCzk) is { } error)
            return Result.Rejected(error);

        try
        {
            var record = await db.CostumeMaintenances.Include(m => m.Item)
                .FirstOrDefaultAsync(m => m.Id == maintenanceId, ct);
            if (record is null)
                return Result.NotFound("Záznam údržby nebyl nalezen.");
            if (record.FinishedAt is not null)
                return Result.Rejected("Údržba už je ukončená.");

            record.FinishedAt = DateTime.UtcNow;
            record.Note = Clean(note) ?? record.Note;
            record.CostCzk = costCzk ?? record.CostCzk;
            record.Item.State = CostumeItemState.Available;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to finish costume maintenance {MaintenanceId}.", maintenanceId);
            return Result.Fail("Ukončení údržby se nepodařilo uložit.");
        }
    }

    public async Task<IReadOnlyList<CostumeMaintenance>> GetForItemAsync(int itemId, CancellationToken ct = default) =>
        await db.CostumeMaintenances.Where(m => m.ItemId == itemId)
            .OrderByDescending(m => m.StartedAt)
            .ToListAsync(ct);

    private static string? ValidateText(string? note, int? costCzk) =>
        note?.Trim().Length > DemizonContext.CostumeTextMaxLength
            ? $"Poznámka může mít nejvýš {DemizonContext.CostumeTextMaxLength} znaků."
            : costCzk < 0 ? "Cena nesmí být záporná." : null;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
