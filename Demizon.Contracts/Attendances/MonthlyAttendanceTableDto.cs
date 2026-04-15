namespace Demizon.Contracts.Attendances;

/// <summary>
/// Full monthly attendance table: column headers + member rows.
/// </summary>
public sealed record MonthlyAttendanceTableDto(
    List<MonthlyColumnDto> Columns,
    List<MemberMonthlyRowDto> Members);
