using Demizon.Core.Services.Costume;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.Services.Costume;

/// <summary>Jedna push zpráva krojárky. <see cref="Data"/> čte mobil pro navigaci po ťuknutí.</summary>
public sealed record CostumePush(int MemberId, string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <summary>
/// Komu a co poslat při změně předávky. Čistá funkce bez odesílání, aby šlo pravidla testovat;
/// posílá <see cref="CostumeWorkflow"/>. Nikdy se neposílá tomu, kdo akci právě udělal.
/// </summary>
public static class CostumeNotifications
{
    /// <summary>Klíč v FCM <c>data</c>: otevřít detail předávky.</summary>
    public const string TransferIdKey = "costumeTransferId";

    /// <summary>Klíč v FCM <c>data</c>: otevřít „Moje kroje“ (připomínka vrácení).</summary>
    public const string MyCostumesKey = "costumes";

    public static IReadOnlyList<CostumePush> ForSubmitted(CostumeTransfer transfer, CostumeTransferStatus result,
        int actorId, IReadOnlyDictionary<int, CostumeMemberName> names)
    {
        var data = TransferData(transfer);
        var codes = Codes(transfer);
        var pushes = new List<CostumePush>();

        if (result == CostumeTransferStatus.Pending && transfer.ToMemberId is { } to)
            pushes.Add(new CostumePush(to, "Převzetí krojů k potvrzení",
                $"{Name(transfer.FromMemberId, names)} ti předává {codes}. Zkontroluj fotky a potvrď převzetí.", data));

        // Vrácení se potvrdí rovnou odesláním — vracející se to dozví tady.
        if (result == CostumeTransferStatus.Confirmed && transfer.FromMemberId is { } from)
            pushes.Add(new CostumePush(from, "Kroje vráceny do krojárky", $"Krojárka převzala {codes}.", data));

        return Without(pushes, actorId);
    }

    /// <param name="keeperIds">Správci krojárky — u předání mezi členy ho jen sledují, neschvalují.</param>
    public static IReadOnlyList<CostumePush> ForConfirmed(CostumeTransfer transfer, int actorId,
        IReadOnlyCollection<int> keeperIds, IReadOnlyDictionary<int, CostumeMemberName> names)
    {
        var data = TransferData(transfer);
        var codes = Codes(transfer);
        var pushes = new List<CostumePush>
        {
            new(transfer.CreatedById, "Převzetí potvrzeno", $"{Name(actorId, names)} potvrdil(a) převzetí {codes}.", data)
        };

        if (transfer is { FromMemberId: { } from, ToMemberId: { } to })
            pushes.AddRange(keeperIds.Select(keeper => new CostumePush(keeper, "Předání krojů mezi členy",
                $"{Name(from, names)} → {Name(to, names)}: {codes}.", data)));

        return Without(pushes, actorId);
    }

    public static IReadOnlyList<CostumePush> ForRejected(CostumeTransfer transfer, int actorId,
        IReadOnlyCollection<int> keeperIds, IReadOnlyDictionary<int, CostumeMemberName> names)
    {
        var data = TransferData(transfer);
        var body = $"{Name(actorId, names)} odmítl(a) převzetí {Codes(transfer)}: {transfer.RejectionReason}";
        var recipients = keeperIds.Prepend(transfer.CreatedById);
        return Without(recipients.Select(id => new CostumePush(id, "Převzetí krojů odmítnuto", body, data)), actorId);
    }

    /// <param name="message">Vlastní text správce; bez něj obecná výzva k vrácení.</param>
    public static CostumePush Reminder(int memberId, IReadOnlyCollection<CostumeHolding> holdings, string? message)
    {
        var pieces = holdings.Sum(h => h.Quantity);
        var list = string.Join(", ", holdings.Select(h => h.Item.Code).Order());
        var intro = string.IsNullOrWhiteSpace(message)
            ? "Domluv se prosím se správcem krojárky na vrácení."
            : message.Trim();
        return new CostumePush(memberId, "Kroje z krojárky", $"{intro} Máš u sebe {pieces} ks: {list}.",
            new Dictionary<string, string> { [MyCostumesKey] = "mine" });
    }

    private static Dictionary<string, string> TransferData(CostumeTransfer transfer) =>
        new() { [TransferIdKey] = transfer.Id.ToString() };

    private static string Codes(CostumeTransfer transfer)
    {
        var codes = transfer.Lines.Select(l => l.Quantity > 1 ? $"{l.Item.Code} ({l.Quantity} ks)" : l.Item.Code)
            .Order().ToList();
        return codes.Count == 1 ? $"kus {codes[0]}" : $"{codes.Count} kusů ({string.Join(", ", codes)})";
    }

    private static string Name(int? memberId, IReadOnlyDictionary<int, CostumeMemberName> names) =>
        memberId is not { } id ? "Krojárka"
        : names.TryGetValue(id, out var name) ? name.FullName
        : "Člen";

    /// <summary>Bez autora akce a bez duplicit (správce může být i zakladatel).</summary>
    private static IReadOnlyList<CostumePush> Without(IEnumerable<CostumePush> pushes, int actorId) =>
        pushes.Where(p => p.MemberId != actorId).DistinctBy(p => p.MemberId).ToList();
}
