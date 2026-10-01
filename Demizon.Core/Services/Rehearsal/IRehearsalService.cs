using Demizon.Common;
using Demizon.Dal.Entities;

namespace Demizon.Core.Services.Rehearsal;

/// <summary>
/// Kalendář pravidelných zkoušek. Data pro <see cref="RehearsalSchedule"/> načte
/// z databáze. Kdo potřebuje vědět, jestli je v nějaký den zkouška, ptá se tady.
/// </summary>
public interface IRehearsalService
{
    /// <summary>Výskyty v rozsahu <c>[from, toExclusive)</c>, včetně zrušených.</summary>
    Task<IReadOnlyList<RehearsalOccurrence>> GetRangeAsync(
        DateOnly from, DateOnly toExclusive, CancellationToken ct = default);

    /// <summary><c>null</c> = v ten den zkouška není (jiný den v týdnu, nebo akce).</summary>
    Task<RehearsalOccurrence?> GetAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>Výjimky, které do rozsahu <c>[from, toExclusive)</c> zasahují, seřazené podle začátku.</summary>
    Task<IReadOnlyList<RehearsalCancellation>> GetCancellationsAsync(
        DateOnly from, DateOnly toExclusive, CancellationToken ct = default);

    Task<RehearsalCancellation?> GetCancellationAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Zruší zkoušky v rozsahu <paramref name="from"/>–<paramref name="to"/> (obojí včetně).
    /// <c>Rejected</c>, když v rozsahu žádná naplánovaná zkouška není.
    /// </summary>
    /// <returns>Klíč nové výjimky.</returns>
    Task<Result<int>> CancelAsync(
        DateOnly from, DateOnly to, string? reason, int? byMemberId, CancellationToken ct = default);

    /// <summary>Smaže výjimku, zkoušky se vrátí i s docházkou, která na ně byla.</summary>
    Task<Result> RestoreAsync(int cancellationId, CancellationToken ct = default);
}
