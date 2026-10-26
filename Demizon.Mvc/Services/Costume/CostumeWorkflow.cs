using Demizon.Common;
using Demizon.Core.Services.Costume;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services.Costume;

/// <summary>
/// Přechody předávek s jejich důsledky mimo databázi (push). Volá ho API i Blazor, aby se
/// obojí chovalo stejně. Selhání pushe předávku nevrací — ta už je uložená.
/// </summary>
public sealed class CostumeWorkflow(
    DemizonContext db,
    ICostumeTransferService transfers,
    ICostumeCatalogService catalog,
    AttendanceReminderService sender,
    ILogger<CostumeWorkflow> logger)
{
    public async Task<Result<CostumeTransferStatus>> SubmitAsync(int transferId, CostumeActor actor,
        CancellationToken ct = default)
    {
        var submitted = await transfers.SubmitAsync(transferId, actor, ct);
        if (submitted.IsSuccess && await transfers.GetAsync(transferId, ct) is { } transfer)
            await SendAsync(CostumeNotifications.ForSubmitted(transfer, submitted.Value, actor.MemberId,
                await NamesAsync(transfer, ct)), ct);
        return submitted;
    }

    public async Task<Result> ConfirmAsync(int transferId, CostumeActor actor, CancellationToken ct = default)
    {
        var confirmed = await transfers.ConfirmAsync(transferId, actor, ct);
        if (confirmed.IsSuccess && await transfers.GetAsync(transferId, ct) is { } transfer)
            await SendAsync(CostumeNotifications.ForConfirmed(transfer, actor.MemberId, await KeeperIdsAsync(ct),
                await NamesAsync(transfer, ct)), ct);
        return confirmed;
    }

    public async Task<Result> RejectAsync(int transferId, CostumeActor actor, string reason, CancellationToken ct = default)
    {
        var rejected = await transfers.RejectAsync(transferId, actor, reason, ct);
        if (rejected.IsSuccess && await transfers.GetAsync(transferId, ct) is { } transfer)
            await SendAsync(CostumeNotifications.ForRejected(transfer, actor.MemberId, await KeeperIdsAsync(ct),
                await NamesAsync(transfer, ct)), ct);
        return rejected;
    }

    /// <summary>
    /// Ruční připomínka vrácení. Bez <paramref name="memberIds"/> všem, kdo něco drží.
    /// Člen, který nic nedrží, se přeskočí.
    /// </summary>
    /// <returns>Komu se připomínka týkala a kolika se ji podařilo doručit aspoň jedním kanálem.</returns>
    public async Task<(int Recipients, int Reached)> RemindAsync(IReadOnlyCollection<int>? memberIds, string? message,
        CancellationToken ct = default)
    {
        var holdings = await catalog.GetHoldingsAsync(ct: ct);
        var pushes = holdings
            .Where(h => memberIds is null || memberIds.Contains(h.MemberId))
            .GroupBy(h => h.MemberId)
            .Select(g => CostumeNotifications.Reminder(g.Key, g.ToList(), message))
            .ToList();
        return (pushes.Count, await SendAsync(pushes, ct));
    }

    private async Task<int> SendAsync(IReadOnlyList<CostumePush> pushes, CancellationToken ct)
    {
        var reached = 0;
        foreach (var push in pushes)
        {
            try
            {
                if (await sender.SendToMemberAsync(push.MemberId, push.Title, push.Body,
                        new Dictionary<string, string>(push.Data), ct))
                    reached++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Costume push „{Title}“ to member {MemberId} failed.", push.Title, push.MemberId);
            }
        }
        return reached;
    }

    private Task<IReadOnlyDictionary<int, CostumeMemberName>> NamesAsync(CostumeTransfer transfer, CancellationToken ct) =>
        catalog.GetMemberNamesAsync(new[] { transfer.FromMemberId, transfer.ToMemberId, transfer.CreatedById,
            transfer.ResolvedById }.OfType<int>(), ct);

    private async Task<IReadOnlyCollection<int>> KeeperIdsAsync(CancellationToken ct) =>
        await db.MemberRoles
            .Where(r => r.Role == UserRole.CostumeKeeper && r.Member.DeletedAt == null)
            .Select(r => r.MemberId)
            .ToListAsync(ct);
}
