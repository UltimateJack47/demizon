namespace Demizon.Contracts.Attendances;

/// <summary>
/// Represents one column in the monthly attendance table: either a Friday rehearsal or a named event.
/// </summary>
public sealed record MonthlyColumnDto(
    int? EventId,
    string Label,
    DateTime Date,
    bool IsEvent,
    bool IsCancelled);
