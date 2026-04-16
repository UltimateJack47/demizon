namespace Demizon.Contracts.Attendances;
public sealed record AttendanceDto(int Id, string Status, string? Comment, string? ActivityRole, DateTime LastUpdated);
