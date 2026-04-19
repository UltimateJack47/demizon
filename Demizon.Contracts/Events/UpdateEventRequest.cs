namespace Demizon.Contracts.Events;

public sealed record UpdateEventRequest(
    string Name,
    DateTime DateFrom,
    DateTime DateTo,
    string? Place,
    int? NotifyBeforeDays,
    string Recurrence,
    bool IsPublic,
    bool IsCancelled);
