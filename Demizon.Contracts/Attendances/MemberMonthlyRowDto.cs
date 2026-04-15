namespace Demizon.Contracts.Attendances;

/// <summary>
/// One member row in the monthly attendance table.
/// </summary>
public sealed record MemberMonthlyRowDto(
    int MemberId,
    string FullName,
    List<MemberCellDto> Cells);
