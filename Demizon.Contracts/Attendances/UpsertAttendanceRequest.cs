namespace Demizon.Contracts.Attendances;
public sealed record UpsertAttendanceRequest(bool Attends, string? Comment, string? ActivityRole);
