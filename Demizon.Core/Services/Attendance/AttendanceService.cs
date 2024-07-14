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

    public async Task UpdateAsync(int id, Dal.Entities.Attendance updatedAttendance)
    {
        var entity = await DemizonContext.Attendances.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Attendance with id: {id} not found.");
        }

        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedAttendance);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Attendance attendance)
    {
        try
        {
            await DemizonContext.AddAsync(attendance);
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
        return await DemizonContext.Attendances.Where(x => x.MemberId == memberId && x.Date >= DateTime.Today)
            .ToListAsync();
    }
}