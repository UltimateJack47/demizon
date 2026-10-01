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
}
