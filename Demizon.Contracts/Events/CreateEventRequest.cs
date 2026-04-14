namespace Demizon.Contracts.Events;

public sealed record CreateEventRequest(
    string Name,
    DateTime DateFrom,
    DateTime DateTo,
    string? Place,
    int? NotifyBeforeDays,
    string Recurrence);
