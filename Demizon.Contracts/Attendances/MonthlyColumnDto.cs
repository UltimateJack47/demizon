namespace Demizon.Contracts.Attendances;

/// <summary>
/// Represents one column in the monthly attendance table: either a Friday rehearsal or a named event.
/// </summary>
/// <param name="IsEvent"><c>EventId != null</c>. Zkouška je sloupec bez akce.</param>
/// <param name="CancellationReason">Důvod zrušené zkoušky („Prázdniny“). Nepovinné, starší klienti ho ignorují.</param>
public sealed record MonthlyColumnDto(
    int? EventId,
    string Label,
    DateTime Date,
    bool IsEvent,
    bool IsCancelled,
    string? CancellationReason = null);
