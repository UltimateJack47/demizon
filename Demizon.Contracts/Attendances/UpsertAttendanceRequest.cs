namespace Demizon.Contracts.Attendances;
public sealed record UpsertAttendanceRequest(string Status, string? Comment, string? ActivityRole);
