using Demizon.Common;
using Demizon.Core.Services.Attendance;
using Demizon.Dal.Entities;

namespace Demizon.Tests.Unit.Fakes;

/// <summary>
/// Obal nad skutečnou <see cref="IAttendanceService"/>, který umí na příkaz
/// nechat zápis selhat. Čtení jde dál na skutečnou službu, protože controller
/// existující docházku potřebuje najít.
/// </summary>
/// <remarks>
/// Proč obal a ne úplný dvojník: kompenzační logika stojí na tom, že se čtení
/// povede a zápis ne. Kdyby dvojník nahradil i čtení, testovala by se jiná
/// větev, než která v provozu dělá potíže.
/// <para>
/// <c>ClearGoogleEventIdAsync</c> se schválně **nechává projít** — to je právě
/// to dorovnání, které se má po selhaném zápisu spustit. Vlastní příznak na
/// jeho selhání existuje zvlášť.
/// </para>
/// </remarks>
public sealed class WriteFailingAttendanceService(IAttendanceService inner, CalendarTestState state)
    : IAttendanceService
{
    public Task<Attendance> GetOneAsync(int id) => inner.GetOneAsync(id);

    public IQueryable<Attendance> GetAll() => inner.GetAll();

    public Task<Result<int>> CreateOrUpdateAsync(Attendance attendance) =>
        state.FailAttendanceWrites
            ? Task.FromResult(Result<int>.Fail("Uložení docházky selhalo (dvojník)."))
            : inner.CreateOrUpdateAsync(attendance);

    public Task<Result> DeleteAsync(int id) =>
        state.FailAttendanceWrites
            ? Task.FromResult(Result.Fail("Smazání docházky selhalo (dvojník)."))
            : inner.DeleteAsync(id);

    public Task<Result> ClearGoogleEventIdAsync(int id)
    {
        state.RecordClearCall();
        return state.FailClearGoogleEventId
            ? Task.FromResult(Result.Fail("Dorovnání selhalo (dvojník)."))
            : inner.ClearGoogleEventIdAsync(id);
    }

    public Task<List<Attendance>> GetMemberAttendancesAsync(int memberId, DateTime dateFrom, DateTime dateTo) =>
        inner.GetMemberAttendancesAsync(memberId, dateFrom, dateTo);

    public Task<List<Attendance>> GetMembersAttendancesAsync(List<int> memberIds, DateTime dateFrom, DateTime dateTo) =>
        inner.GetMembersAttendancesAsync(memberIds, dateFrom, dateTo);
}
