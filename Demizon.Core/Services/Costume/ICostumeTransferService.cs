using Demizon.Common;
using Demizon.Dal.Entities;

namespace Demizon.Core.Services.Costume;

/// <summary>Fotka bez dat, pro výpis předávky.</summary>
/// <param name="IsStale">Pořízená o víc než nastavený limit dřív, než byla nahrána.</param>
public sealed record CostumePhotoInfo(int Id, int TransferLineId, DateTime UploadedAt, DateTime? CapturedAt, bool IsStale);

/// <summary>
/// Předávky kusů. Stavový automat <c>Draft → Pending → Confirmed / Rejected</c>, kdykoli předtím
/// <c>Cancelled</c>. Držení se mění jen potvrzením a potvrzuje vždy příjemce; vrácení do krojárky
/// zakládá správce, který je zároveň příjemcem, takže se potvrdí rovnou při odeslání.
/// </summary>
public interface ICostumeTransferService
{
    /// <param name="fromMemberId">null = z krojárky (smí jen správce).</param>
    /// <param name="toMemberId">null = do krojárky (zakládá jen správce).</param>
    Task<Result<int>> CreateDraftAsync(CostumeActor actor, int? fromMemberId, int? toMemberId,
        IReadOnlyList<CostumeLineInput> lines, string? note, CancellationToken ct = default);

    Task<Result<int>> AddLineAsync(int transferId, CostumeActor actor, CostumeLineInput line,
        CancellationToken ct = default);

    Task<Result> RemoveLineAsync(int transferId, int lineId, CostumeActor actor, CancellationToken ct = default);

    Task<Result> SetLineNoteAsync(int transferId, int lineId, CostumeActor actor, string? conditionNote,
        CancellationToken ct = default);

    Task<Result<int>> AddPhotoAsync(int transferId, int lineId, CostumeActor actor, Stream photo,
        CancellationToken ct = default);

    Task<Result> RemovePhotoAsync(int transferId, int photoId, CostumeActor actor, CancellationToken ct = default);

    /// <summary>Vrací výsledný stav: <c>Pending</c>, nebo <c>Confirmed</c> u vrácení do krojárky.</summary>
    Task<Result<CostumeTransferStatus>> SubmitAsync(int transferId, CostumeActor actor, CancellationToken ct = default);

    Task<Result> ConfirmAsync(int transferId, CostumeActor actor, CancellationToken ct = default);

    Task<Result> RejectAsync(int transferId, CostumeActor actor, string reason, CancellationToken ct = default);

    /// <summary>Rozpracovanou nebo čekající předávku zruší její zakladatel nebo správce.</summary>
    Task<Result> CancelAsync(int transferId, CostumeActor actor, CancellationToken ct = default);

    /// <summary>Předávka s řádky a kusy; fotky jen jako metadata přes <see cref="GetPhotoInfosAsync"/>.</summary>
    Task<CostumeTransfer?> GetAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<CostumePhotoInfo>> GetPhotoInfosAsync(int transferId, CancellationToken ct = default);

    /// <summary>Data fotky; null, když neexistuje nebo nepatří k předávce.</summary>
    Task<byte[]?> GetPhotoDataAsync(int transferId, int photoId, bool thumbnail, CancellationToken ct = default);

    /// <summary>Předávky, kde člen předává, přijímá nebo je založil; nejnovější první.</summary>
    Task<IReadOnlyList<CostumeTransfer>> GetForMemberAsync(int memberId, CancellationToken ct = default);

    /// <summary>Rozpracované a čekající předávky (přehled správce).</summary>
    Task<IReadOnlyList<CostumeTransfer>> GetOpenAsync(CancellationToken ct = default);

    /// <summary>Všechny předávky, ve kterých kus byl; nejnovější první.</summary>
    Task<IReadOnlyList<CostumeTransfer>> GetItemHistoryAsync(int itemId, CancellationToken ct = default);
}
