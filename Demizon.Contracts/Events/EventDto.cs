using Demizon.Contracts.Attendances;

namespace Demizon.Contracts.Events;
public sealed record EventDto(
    int Id, string Name, DateTime DateFrom, DateTime DateTo,
    string? Place, bool IsCancelled, string Recurrence,
    AttendanceDto? MyAttendance = null,
    bool IsRehearsal = false,
    bool IsPublic = false,
    int? NotifyBeforeDays = null);
