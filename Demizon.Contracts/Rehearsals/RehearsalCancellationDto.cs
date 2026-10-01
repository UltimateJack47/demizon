namespace Demizon.Contracts.Rehearsals;

/// <summary>Výjimka z pravidelných zkoušek. Obě data včetně.</summary>
public sealed record RehearsalCancellationDto(
    int Id,
    DateOnly DateFrom,
    DateOnly DateTo,
    string? Reason,
    DateTime CreatedAt,
    string? CreatedBy);
