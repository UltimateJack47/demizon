namespace Demizon.Contracts.Attendances;

public sealed record MemberAttendanceStatDto(
    int MemberId,
    string FullName,
    int TotalRehearsals,
    int AttendedRehearsals,
    double RehearsalRate,
    int TotalActions,
    int AttendedActions,
    double ActionRate
);
