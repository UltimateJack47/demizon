using Demizon.Common.Exceptions;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Attendance;

public class AttendanceService(DemizonContext demizonContext, ILogger<AttendanceService> logger) : IAttendanceService
{
    private DemizonContext DemizonContext { get; } = demizonContext;

    public async Task<Dal.Entities.Attendance> GetOneAsync(int id)
    {
        return await DemizonContext.Attendances.FindAsync(id) ??
               throw new EntityNotFoundException($"Attendance with id: {id} not found.");
    }

    public IQueryable<Dal.Entities.Attendance> GetAll()
    {
        return DemizonContext.Attendances.AsQueryable();
    }

    public async Task<Common.Result<int>> CreateOrUpdateAsync(Dal.Entities.Attendance attendance)
    {
        attendance.LastUpdated = DateTime.Now;
        try
        {
            if (attendance.Id != 0)
            {
                var dbEntity = await DemizonContext.Attendances.FindAsync(attendance.Id);
                if (dbEntity is not null)
                {
                    DemizonContext.Entry(dbEntity).CurrentValues.SetValues(attendance);
                    DemizonContext.Entry(dbEntity).State = EntityState.Modified;
                }
                else
                {
                    await DemizonContext.AddAsync(attendance);
                }
            }
            else
            {
                await DemizonContext.AddAsync(attendance);
            }

            await DemizonContext.SaveChangesAsync();
            // Klíč vrací služba sama, takže volající nemusí spoléhat na to, že mu
            // ho EF dopsal do předané entity (na tom stála oprava osiřelých
            // událostí v Google Calendaru).
            return Common.Result<int>.Ok(attendance.Id);
        }
        catch (Exception ex)
        {
            // Na cestě update je trackovaná načtená entita, ne ta předaná — proto
            // se maže celý tracker a ne konkrétní instance. Jinak by se neúspěšný
            // zápis přehrál při příštím nesouvisejícím SaveChanges v tomtéž okruhu.
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process Attendance operation.");
            return Common.Result<int>.Fail("Docházku se nepodařilo uložit.");
        }
    }

    public async Task<Common.Result> DeleteAsync(int id)
    {
        try
        {
            var entity = await DemizonContext.Attendances.FindAsync(id);
            if (entity is null)
            {
                return Common.Result.NotFound("Docházka nebyla nalezena.");
            }

            DemizonContext.Attendances.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return Common.Result.Ok();
        }
        catch (Exception ex)
        {
            // Vrátí tracker do čistého stavu, jinak by se smazání přehrálo
            // při příštím uložení v tomtéž okruhu.
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process Attendance operation.");
            return Common.Result.Fail("Docházku se nepodařilo smazat.");
        }
    }

    public async Task<List<Dal.Entities.Attendance>> GetMemberAttendancesAsync(int memberId, DateTime dateFrom,
        DateTime dateTo)
    {
        return await DemizonContext.Attendances
            .Include(x => x.Event)
            .Where(x => x.MemberId == memberId && x.Date >= dateFrom && x.Date <= dateTo)
            .ToListAsync();
    }

    public async Task<List<Dal.Entities.Attendance>> GetMembersAttendancesAsync(List<int> memberIds, DateTime dateFrom,
        DateTime dateTo)
    {
        return await DemizonContext.Attendances
            .Include(x => x.Event)
            .Where(x => memberIds.Contains(x.MemberId) && x.Date >= dateFrom && x.Date <= dateTo)
            .ToListAsync();
    }
}
