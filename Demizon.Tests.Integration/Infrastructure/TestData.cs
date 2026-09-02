using Demizon.Dal;
using Demizon.Dal.Entities;

namespace Demizon.Tests.Integration.Infrastructure;

/// <summary>
/// Builders pro testovací data. Drží na jednom místě povinné vlastnosti entit,
/// aby test říkal jen to, na čem mu skutečně záleží.
/// </summary>
public static class TestData
{
    public static Member Member(
        string login = "tester",
        string name = "Jan",
        string surname = "Novak",
        UserRole role = UserRole.Standard,
        bool isAttendanceVisible = true,
        bool isExternal = false) => new()
    {
        Name = name,
        Surname = surname,
        Login = login,
        Email = $"{login}@demizon.test",
        PasswordHash = "not-a-real-hash",
        Role = role,
        Gender = Gender.Male,
        IsAttendanceVisible = isAttendanceVisible,
        IsExternal = isExternal
    };

    public static Event Event(
        string name = "Vystoupeni",
        DateTime? dateFrom = null,
        DateTime? dateTo = null) => new()
    {
        Name = name,
        DateFrom = dateFrom ?? new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
        DateTo = dateTo ?? new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>Docházka na akci — má <c>EventId</c>.</summary>
    public static Attendance ActionAttendance(int memberId, int eventId, DateTime date, AttendanceStatus status) => new()
    {
        MemberId = memberId,
        EventId = eventId,
        Date = date,
        Status = status
    };

    /// <summary>
    /// Docházka na zkoušku. Zkoušky nemají řádek v <c>Events</c> — modelují se jako
    /// docházka s <c>EventId == null</c> (viz AGENTS.md).
    /// </summary>
    public static Attendance RehearsalAttendance(int memberId, DateTime date, AttendanceStatus status) => new()
    {
        MemberId = memberId,
        EventId = null,
        Date = date,
        Status = status
    };

    public static async Task<Member> SeedMemberAsync(DemizonContext db, string login = "tester",
        bool isAttendanceVisible = true)
    {
        var member = Member(login: login, isAttendanceVisible: isAttendanceVisible);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member;
    }
}
