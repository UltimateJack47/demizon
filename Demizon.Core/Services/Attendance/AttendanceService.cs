using Demizon.Common.Exceptions;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Attendance;

public class AttendanceService(DemizonContext demizonContext) : IAttendanceService
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

    public async Task<bool> CreateOrUpdateAsync(Dal.Entities.Attendance attendance)
    {
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
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var entity = await DemizonContext.Attendances.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }

            DemizonContext.Attendances.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<Dal.Entities.Attendance>> GetMemberAttendancesAsync(int memberId)
    {
        return await DemizonContext.Attendances
            .Include(x => x.Event)
            .Where(x => x.MemberId == memberId && x.Date >= DateTime.Today)
            .ToListAsync();
    }
}
