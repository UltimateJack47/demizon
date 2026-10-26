using Demizon.Contracts.Costumes;
using Demizon.Core.Services.Costume;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.Mapping;

/// <summary>
/// Krojárka → kontrakty. Jména členů chodí zvenku (<see cref="ICostumeCatalogService.GetMemberNamesAsync"/>),
/// protože entity krojárky na člena navigaci záměrně nemají.
/// </summary>
public static class CostumeContractMapping
{
    /// <param name="includeValue">Hodnotu kusu vidí jen ten, kdo vidí celou krojárku.</param>
    public static CostumeItemDto ToDto(this CostumeItem i, bool includeValue) =>
        new(i.Id, i.Code, i.PartId, i.Part.Name, i.Part.Costume?.Name, i.Part.Gender.ToString(),
            i.Part.TrackingMode.ToString(), i.Size, i.Color, i.Note, includeValue ? i.ValueCzk : null,
            i.Quantity, i.State.ToString());

    public static CostumeDto ToDto(this Dal.Entities.Costume c) => new(c.Id, c.Name, c.Description, c.SortOrder);

    public static CostumePartyDto ToParty(this IReadOnlyDictionary<int, CostumeMemberName> names, int memberId) =>
        names.TryGetValue(memberId, out var name)
            ? new CostumePartyDto(memberId, name.FullName, name.IsDeleted)
            : new CostumePartyDto(memberId, "Neznámý člen", true);

    public static IEnumerable<int> MemberIds(this CostumeTransfer t) =>
        new[] { t.FromMemberId, t.ToMemberId, t.CreatedById, t.ResolvedById }.OfType<int>();

    public static CostumeTransferDto ToDto(this CostumeTransfer t, IReadOnlyDictionary<int, CostumeMemberName> names,
        IReadOnlyList<CostumePhotoInfo> photos, bool includeValue) =>
        new(t.Id, t.Status.ToString(),
            t.FromMemberId is { } from ? names.ToParty(from) : null,
            t.ToMemberId is { } to ? names.ToParty(to) : null,
            names.ToParty(t.CreatedById),
            Utc(t.CreatedAt), Utc(t.SubmittedAt), Utc(t.ResolvedAt),
            t.ResolvedById is { } resolvedBy ? names.ToParty(resolvedBy) : null,
            t.Note, t.RejectionReason,
            t.Lines.OrderBy(l => l.Item.Code).Select(l => new CostumeTransferLineDto(
                l.Id, l.Item.ToDto(includeValue), l.Quantity, l.ConditionNote,
                photos.Where(p => p.TransferLineId == l.Id)
                    .Select(p => new CostumePhotoDto(p.Id, p.UploadedAt, p.CapturedAt, p.IsStale))
                    .ToList())).ToList());

    // SQLite DateTimeKind neuchová; bez Utc by se čas serializoval bez „Z“ a klient ho četl jako místní.
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
}
