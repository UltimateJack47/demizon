namespace Demizon.Contracts.Attendances;
public sealed record AttendanceDto(int Id, bool Attends, string? Comment, string? ActivityRole, DateTime LastUpdated);
