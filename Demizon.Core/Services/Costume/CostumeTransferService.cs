using Demizon.Common;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Costume;

public sealed class CostumeTransferService(
    DemizonContext db,
    CostumePhotoProcessor photoProcessor,
    ILogger<CostumeTransferService> logger) : ICostumeTransferService
{
    private static readonly CostumeTransferStatus[] OpenStatuses = CostumeCatalogService.OpenStatuses;

    // ------------------------------------------------------------ zakládání

    public async Task<Result<int>> CreateDraftAsync(CostumeActor actor, int? fromMemberId, int? toMemberId,
        IReadOnlyList<CostumeLineInput> lines, string? note, CancellationToken ct = default)
    {
        if (CheckCreateRights(actor, fromMemberId, toMemberId) is { } rightsError)
            return Result<int>.Rejected(rightsError);
        if (lines.Count == 0)
            return Result<int>.Rejected("Předávka musí mít aspoň jeden kus.");
        if (note?.Trim().Length > DemizonContext.CostumeTextMaxLength)
            return Result<int>.Rejected($"Poznámka může mít nejvýš {DemizonContext.CostumeTextMaxLength} znaků.");

        try
        {
            if (await CheckPartiesAsync(fromMemberId, toMemberId, ct) is { } partyError)
                return Result<int>.Rejected(partyError);

            var transfer = new CostumeTransfer
            {
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                CreatedById = actor.MemberId,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            };

            foreach (var line in lines)
            {
                var built = await BuildLineAsync(transfer, line, ct);
                if (!built.IsSuccess)
                    return Result<int>.Fail(built.Error!, built.ErrorKind);
                transfer.Lines.Add(built.Value!);
            }

            db.CostumeTransfers.Add(transfer);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(transfer.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to create costume transfer by member {MemberId}.", actor.MemberId);
            return Result<int>.Fail("Předávku se nepodařilo založit.");
        }
    }

    private static string? CheckCreateRights(CostumeActor actor, int? from, int? to)
    {
        if (from is null && to is null)
            return "Z krojárky do krojárky se nepředává.";
        if (from == to)
            return "Kusy nejde předat témuž člověku.";
        if (actor.IsKeeper)
            return null;
        if (from != actor.MemberId)
            return "Předat můžeš jen kusy, které máš u sebe.";
        if (to is null)
            return "Vrácení do krojárky zakládá správce krojárky.";
        return null;
    }

    private async Task<string?> CheckPartiesAsync(int? from, int? to, CancellationToken ct)
    {
        // Předávající smí být i bývalý člen — správce od něj kroj přebírá zpátky.
        if (from is { } fromId && !await db.Members.IgnoreQueryFilters().AnyAsync(m => m.Id == fromId, ct))
            return "Předávající člen nebyl nalezen.";
        if (to is { } toId && !await db.Members.AnyAsync(m => m.Id == toId && !m.IsExternal, ct))
            return "Příjemce nebyl nalezen nebo se nemůže přihlásit, takže by převzetí nepotvrdil.";
        return null;
    }

    /// <summary>
    /// Řádek pro kus z <paramref name="transfer"/>: kus musí být dostupný, u předávajícího a ne
    /// zamčený v jiné otevřené předávce.
    /// </summary>
    private async Task<Result<CostumeTransferLine>> BuildLineAsync(
        CostumeTransfer transfer, CostumeLineInput input, CancellationToken ct)
    {
        var code = CostumeCodes.Normalize(input.Code);
        var item = await db.CostumeItems.Include(i => i.Part).FirstOrDefaultAsync(i => i.Code == code, ct);
        if (item is null)
            return Result<CostumeTransferLine>.Rejected($"Kus {code} neexistuje.");
        if (transfer.Lines.Any(l => l.ItemId == item.Id))
            return Result<CostumeTransferLine>.Rejected($"Kus {code} už v předávce je.");

        var quantity = item.Part.TrackingMode == CostumeTrackingMode.Individual ? 1 : input.Quantity;
        if (quantity < 1)
            return Result<CostumeTransferLine>.Rejected("Počet musí být aspoň 1.");

        if (await CheckAvailableAsync(item, transfer.FromMemberId, quantity, transfer.Id, ct) is { } error)
            return Result<CostumeTransferLine>.Rejected(error);

        return Result<CostumeTransferLine>.Ok(new CostumeTransferLine { ItemId = item.Id, Item = item, Quantity = quantity });
    }

    private async Task<string?> CheckAvailableAsync(
        CostumeItem item, int? fromMemberId, int quantity, int excludeTransferId, CancellationToken ct)
    {
        if (item.State != CostumeItemState.Available)
            return $"Kus {item.Code} {StateText(item.State)}.";

        var held = fromMemberId is { } memberId
            ? await db.CostumeHoldings.Where(h => h.ItemId == item.Id && h.MemberId == memberId)
                .SumAsync(h => h.Quantity, ct)
            : item.Quantity - await db.CostumeHoldings.Where(h => h.ItemId == item.Id).SumAsync(h => h.Quantity, ct);

        var reserved = await db.CostumeTransferLines
            .Where(l => l.ItemId == item.Id
                        && l.TransferId != excludeTransferId
                        && l.Transfer.FromMemberId == fromMemberId
                        && OpenStatuses.Contains(l.Transfer.Status))
            .SumAsync(l => l.Quantity, ct);

        if (held == 0)
            return fromMemberId is null
                ? $"Kus {item.Code} není v krojárce."
                : $"Kus {item.Code} předávající u sebe nemá.";
        if (quantity > held - reserved)
            return item.Part.TrackingMode == CostumeTrackingMode.Individual
                ? $"Kus {item.Code} už je v jiné předávce."
                : $"Z {item.Code} je k předání jen {Math.Max(0, held - reserved)} ks.";
        return null;
    }

    private static string StateText(CostumeItemState state) => state switch
    {
        CostumeItemState.InMaintenance => "je v údržbě",
        CostumeItemState.Lost => "je vedený jako ztracený",
        CostumeItemState.Retired => "je vyřazený",
        _ => "není dostupný"
    };

    // ------------------------------------------------------- úpravy draftu

    public async Task<Result<int>> AddLineAsync(int transferId, CostumeActor actor, CostumeLineInput line,
        CancellationToken ct = default)
    {
        try
        {
            var transfer = await LoadDraftAsync(transferId, actor, ct);
            if (!transfer.IsSuccess)
                return Result<int>.Fail(transfer.Error!, transfer.ErrorKind);

            var built = await BuildLineAsync(transfer.Value!, line, ct);
            if (!built.IsSuccess)
                return Result<int>.Fail(built.Error!, built.ErrorKind);

            transfer.Value!.Lines.Add(built.Value!);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(built.Value!.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to add line to costume transfer {TransferId}.", transferId);
            return Result<int>.Fail("Kus se nepodařilo přidat.");
        }
    }

    public async Task<Result> RemoveLineAsync(int transferId, int lineId, CostumeActor actor, CancellationToken ct = default)
    {
        try
        {
            var transfer = await LoadDraftAsync(transferId, actor, ct);
            if (!transfer.IsSuccess)
                return Result.Fail(transfer.Error!, transfer.ErrorKind);

            var line = transfer.Value!.Lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.NotFound("Kus v předávce není.");
            if (transfer.Value.Lines.Count == 1)
                return Result.Rejected("Poslední kus nejde odebrat. Zruš raději celou předávku.");

            db.CostumeTransferLines.Remove(line);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to remove line {LineId} from costume transfer {TransferId}.", lineId, transferId);
            return Result.Fail("Kus se nepodařilo odebrat.");
        }
    }

    public async Task<Result> SetLineNoteAsync(int transferId, int lineId, CostumeActor actor, string? conditionNote,
        CancellationToken ct = default)
    {
        if (conditionNote?.Trim().Length > DemizonContext.CostumeTextMaxLength)
            return Result.Rejected($"Poznámka může mít nejvýš {DemizonContext.CostumeTextMaxLength} znaků.");

        try
        {
            var transfer = await LoadDraftAsync(transferId, actor, ct);
            if (!transfer.IsSuccess)
                return Result.Fail(transfer.Error!, transfer.ErrorKind);

            var line = transfer.Value!.Lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
                return Result.NotFound("Kus v předávce není.");

            line.ConditionNote = string.IsNullOrWhiteSpace(conditionNote) ? null : conditionNote.Trim();
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to set note of line {LineId} in costume transfer {TransferId}.", lineId, transferId);
            return Result.Fail("Poznámku se nepodařilo uložit.");
        }
    }

    public async Task<Result<int>> AddPhotoAsync(int transferId, int lineId, CostumeActor actor, Stream photo,
        CancellationToken ct = default)
    {
        try
        {
            var transfer = await LoadDraftAsync(transferId, actor, ct);
            if (!transfer.IsSuccess)
                return Result<int>.Fail(transfer.Error!, transfer.ErrorKind);
            if (transfer.Value!.Lines.All(l => l.Id != lineId))
                return Result<int>.NotFound("Kus v předávce není.");

            var processed = await photoProcessor.ProcessAsync(photo, actor.MemberId, ct);
            if (!processed.IsSuccess)
                return Result<int>.Fail(processed.Error!, processed.ErrorKind);

            var entity = processed.Value!;
            entity.TransferLineId = lineId;
            db.CostumePhotos.Add(entity);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(entity.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to add photo to line {LineId} of costume transfer {TransferId}.", lineId, transferId);
            return Result<int>.Fail("Fotku se nepodařilo uložit.");
        }
    }

    public async Task<Result> RemovePhotoAsync(int transferId, int photoId, CostumeActor actor, CancellationToken ct = default)
    {
        try
        {
            var transfer = await LoadDraftAsync(transferId, actor, ct);
            if (!transfer.IsSuccess)
                return Result.Fail(transfer.Error!, transfer.ErrorKind);

            var deleted = await db.CostumePhotos
                .Where(p => p.Id == photoId && p.TransferLine!.TransferId == transferId)
                .ExecuteDeleteAsync(ct);
            return deleted == 0 ? Result.NotFound("Fotka v předávce není.") : Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to remove photo {PhotoId} from costume transfer {TransferId}.", photoId, transferId);
            return Result.Fail("Fotku se nepodařilo smazat.");
        }
    }

    /// <summary>Rozpracovaná předávka, kterou smí upravovat jen ten, kdo ji založil.</summary>
    private async Task<Result<CostumeTransfer>> LoadDraftAsync(int transferId, CostumeActor actor, CancellationToken ct)
    {
        var transfer = await db.CostumeTransfers
            .Include(t => t.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);
        if (transfer is null)
            return Result<CostumeTransfer>.NotFound("Předávka nebyla nalezena.");
        if (transfer.CreatedById != actor.MemberId)
            return Result<CostumeTransfer>.Rejected("Předávku upravuje jen ten, kdo ji založil.");
        if (transfer.Status != CostumeTransferStatus.Draft)
            return Result<CostumeTransfer>.Rejected("Předávka už je odeslaná.");
        return Result<CostumeTransfer>.Ok(transfer);
    }

    // ------------------------------------------------------ přechody stavu

    public async Task<Result<CostumeTransferStatus>> SubmitAsync(int transferId, CostumeActor actor,
        CancellationToken ct = default)
    {
        try
        {
            var loaded = await LoadDraftAsync(transferId, actor, ct);
            if (!loaded.IsSuccess)
                return Result<CostumeTransferStatus>.Fail(loaded.Error!, loaded.ErrorKind);
            var transfer = loaded.Value!;

            var lineIds = transfer.Lines.Select(l => l.Id).ToList();
            var photographed = await db.CostumePhotos
                .Where(p => p.TransferLineId != null && lineIds.Contains(p.TransferLineId.Value))
                .Select(p => p.TransferLineId!.Value)
                .Distinct()
                .ToListAsync(ct);
            var missing = transfer.Lines.Where(l => !photographed.Contains(l.Id)).Select(l => l.Item.Code).ToList();
            if (missing.Count > 0)
                return Result<CostumeTransferStatus>.Rejected(
                    $"Chybí fotka u {string.Join(", ", missing)}. Bez fotky stavu nejde kus předat.");

            transfer.SubmittedAt = DateTime.UtcNow;

            // Vrácení do krojárky: příjemcem je správce, který předávku založil — jeho odeslání
            // je zároveň potvrzení.
            if (transfer.ToMemberId is null)
            {
                var applied = await ApplyAsync(transfer, actor, ct);
                return applied.IsSuccess
                    ? Result<CostumeTransferStatus>.Ok(CostumeTransferStatus.Confirmed)
                    : Result<CostumeTransferStatus>.Fail(applied.Error!, applied.ErrorKind);
            }

            transfer.Status = CostumeTransferStatus.Pending;
            await db.SaveChangesAsync(ct);
            return Result<CostumeTransferStatus>.Ok(CostumeTransferStatus.Pending);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to submit costume transfer {TransferId}.", transferId);
            return Result<CostumeTransferStatus>.Fail("Předávku se nepodařilo odeslat.");
        }
    }

    public async Task<Result> ConfirmAsync(int transferId, CostumeActor actor, CancellationToken ct = default)
    {
        try
        {
            var transfer = await db.CostumeTransfers
                .Include(t => t.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Part)
                .FirstOrDefaultAsync(t => t.Id == transferId, ct);
            if (transfer is null)
                return Result.NotFound("Předávka nebyla nalezena.");
            if (!IsRecipient(transfer, actor))
                return Result.Rejected("Převzetí potvrzuje jen příjemce.");
            if (transfer.Status != CostumeTransferStatus.Pending)
                return Result.Rejected("Předávka nečeká na potvrzení.");

            return await ApplyAsync(transfer, actor, ct);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to confirm costume transfer {TransferId}.", transferId);
            return Result.Fail("Převzetí se nepodařilo potvrdit.");
        }
    }

    /// <summary>
    /// Přepíše držení podle řádků a předávku uzavře. Dostupnost se ověřuje znovu: mezi odesláním
    /// a potvrzením mohl správce kus vyřadit nebo poslat do údržby.
    /// </summary>
    private async Task<Result> ApplyAsync(CostumeTransfer transfer, CostumeActor actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var line in transfer.Lines)
        {
            if (await CheckAvailableAsync(line.Item, transfer.FromMemberId, line.Quantity, transfer.Id, ct) is { } error)
            {
                db.DiscardPendingChanges();
                return Result.Rejected(error);
            }

            if (transfer.FromMemberId is { } from)
            {
                var source = await db.CostumeHoldings.SingleAsync(h => h.ItemId == line.ItemId && h.MemberId == from, ct);
                source.Quantity -= line.Quantity;
                if (source.Quantity == 0)
                    db.CostumeHoldings.Remove(source);
            }

            if (transfer.ToMemberId is { } to)
            {
                var target = await db.CostumeHoldings.FirstOrDefaultAsync(h => h.ItemId == line.ItemId && h.MemberId == to, ct);
                if (target is null)
                    db.CostumeHoldings.Add(new CostumeHolding { ItemId = line.ItemId, MemberId = to, Quantity = line.Quantity });
                else
                    target.Quantity += line.Quantity;
            }
        }

        transfer.Status = CostumeTransferStatus.Confirmed;
        transfer.ResolvedAt = DateTime.UtcNow;
        transfer.ResolvedById = actor.MemberId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(int transferId, CostumeActor actor, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Rejected("Napiš, proč převzetí odmítáš — správce podle toho bude řešit stav kusů.");
        if (reason.Trim().Length > DemizonContext.CostumeTextMaxLength)
            return Result.Rejected($"Důvod může mít nejvýš {DemizonContext.CostumeTextMaxLength} znaků.");

        try
        {
            var transfer = await db.CostumeTransfers.FirstOrDefaultAsync(t => t.Id == transferId, ct);
            if (transfer is null)
                return Result.NotFound("Předávka nebyla nalezena.");
            if (!IsRecipient(transfer, actor))
                return Result.Rejected("Převzetí odmítá jen příjemce.");
            if (transfer.Status != CostumeTransferStatus.Pending)
                return Result.Rejected("Předávka nečeká na potvrzení.");

            transfer.Status = CostumeTransferStatus.Rejected;
            transfer.RejectionReason = reason.Trim();
            transfer.ResolvedAt = DateTime.UtcNow;
            transfer.ResolvedById = actor.MemberId;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to reject costume transfer {TransferId}.", transferId);
            return Result.Fail("Odmítnutí se nepodařilo uložit.");
        }
    }

    public async Task<Result> CancelAsync(int transferId, CostumeActor actor, CancellationToken ct = default)
    {
        try
        {
            var transfer = await db.CostumeTransfers.FirstOrDefaultAsync(t => t.Id == transferId, ct);
            if (transfer is null)
                return Result.NotFound("Předávka nebyla nalezena.");
            if (transfer.CreatedById != actor.MemberId && !actor.IsKeeper)
                return Result.Rejected("Předávku zruší jen ten, kdo ji založil, nebo správce krojárky.");
            if (!OpenStatuses.Contains(transfer.Status))
                return Result.Rejected("Uzavřenou předávku už nejde zrušit.");

            transfer.Status = CostumeTransferStatus.Cancelled;
            transfer.ResolvedAt = DateTime.UtcNow;
            transfer.ResolvedById = actor.MemberId;
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to cancel costume transfer {TransferId}.", transferId);
            return Result.Fail("Předávku se nepodařilo zrušit.");
        }
    }

    private static bool IsRecipient(CostumeTransfer transfer, CostumeActor actor) =>
        transfer.ToMemberId is { } to ? to == actor.MemberId : actor.IsKeeper;

    // --------------------------------------------------------------- čtení

    private IQueryable<CostumeTransfer> WithLines() =>
        db.CostumeTransfers.Include(t => t.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Part)
            .ThenInclude(p => p.Costume);

    public Task<CostumeTransfer?> GetAsync(int id, CancellationToken ct = default) =>
        WithLines().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<CostumePhotoInfo>> GetPhotoInfosAsync(int transferId, CancellationToken ct = default)
    {
        var photos = await db.CostumePhotos
            .Where(p => p.TransferLine!.TransferId == transferId)
            .OrderBy(p => p.UploadedAt)
            .Select(p => new { p.Id, LineId = p.TransferLineId!.Value, p.UploadedAt, p.CapturedAt })
            .ToListAsync(ct);
        // SQLite DateTimeKind neuchová; bez Utc by se čas serializoval bez „Z“ a klient ho četl jako místní.
        return photos
            .Select(p => new CostumePhotoInfo(p.Id, p.LineId,
                DateTime.SpecifyKind(p.UploadedAt, DateTimeKind.Utc),
                p.CapturedAt is { } captured ? DateTime.SpecifyKind(captured, DateTimeKind.Utc) : null,
                photoProcessor.IsStale(p.UploadedAt, p.CapturedAt)))
            .ToList();
    }

    public Task<byte[]?> GetPhotoDataAsync(int transferId, int photoId, bool thumbnail, CancellationToken ct = default) =>
        db.CostumePhotos
            .Where(p => p.Id == photoId && p.TransferLine!.TransferId == transferId)
            .Select(p => thumbnail ? p.ThumbnailData : p.Data)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CostumeTransfer>> GetForMemberAsync(int memberId, CancellationToken ct = default) =>
        await WithLines()
            .Where(t => t.FromMemberId == memberId || t.ToMemberId == memberId || t.CreatedById == memberId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CostumeTransfer>> GetOpenAsync(CancellationToken ct = default) =>
        await WithLines()
            .Where(t => OpenStatuses.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CostumeTransfer>> GetItemHistoryAsync(int itemId, CancellationToken ct = default) =>
        await WithLines()
            .Where(t => t.Lines.Any(l => l.ItemId == itemId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
}
