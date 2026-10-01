namespace Demizon.Dal.Entities;

/// <summary>
/// Výjimka z pravidelných zkoušek: v rozsahu <see cref="DateFrom"/>–<see cref="DateTo"/>
/// zkouška odpadá. Jedna zkouška má <c>DateFrom == DateTo</c>, prázdniny celé období.
/// </summary>
/// <remarks>
/// Docházka na zrušený den se nemaže. Dokud výjimka platí, jen se nezobrazuje
/// a nepočítá. Smazáním výjimky se vrátí, jak byla.
/// </remarks>
public class RehearsalCancellation
{
    public int Id { get; set; }

    /// <summary>Včetně.</summary>
    public DateOnly DateFrom { get; set; }

    /// <summary>Včetně.</summary>
    public DateOnly DateTo { get; set; }

    /// <summary>Zobrazí se členům („Prázdniny“, „Hody v Lanžhotě“).</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedByMemberId { get; set; }

    public virtual Member? CreatedBy { get; set; }
}
