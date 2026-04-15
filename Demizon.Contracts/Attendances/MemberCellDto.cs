namespace Demizon.Contracts.Attendances;

/// <summary>
/// Attendance value for a single member × column cell in the monthly table.
/// </summary>
public sealed record MemberCellDto(
    DateTime Date,
    int? EventId,
    bool? Attends);
