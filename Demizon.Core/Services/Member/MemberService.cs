using Demizon.Common.Exceptions;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Member;

public class MemberService(DemizonContext demizonContext, ILogger<MemberService> logger) : IMemberService
{
    private DemizonContext DemizonContext { get; set; } = demizonContext;

    public async Task<Dal.Entities.Member> GetOneAsync(int id)
    {
        return await DemizonContext.Members.FindAsync(id) ??
               throw new EntityNotFoundException($"Member with id: {id} not found.");
    }

    public Dal.Entities.Member? GetOneByLogin(string? login)
    {
        return DemizonContext.Members.FirstOrDefault(x => x.Login == login);
    }

    public IQueryable<Dal.Entities.Member> GetAll()
    {
        return DemizonContext.Members.Where(x => x.DeletedAt == null);
    }

    public async Task UpdateAsync(int id, Dal.Entities.Member updatedMember)
    {
        var entity = await DemizonContext.Members.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Member with id: {id} not found.");
        }

        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedMember);
        entity.Photos = updatedMember.Photos;
        DemizonContext.Entry(entity).State = EntityState.Modified;

        // Google tokeny jsou spravovány dedikovanými metodami (Connect/Disconnect) – nikdy je nepřepisuj z formuláře
        var entry = DemizonContext.Entry(entity);
        entry.Property(m => m.GoogleRefreshToken).IsModified = false;
        entry.Property(m => m.GoogleCalendarId).IsModified = false;
        entry.Property(m => m.GoogleConnectedAt).IsModified = false;

        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Member member)
    {
        try
        {
            await DemizonContext.AddAsync(member);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Bez tohohle by entita zůstala Added a vložila se při příštím uložení
            // v tomtéž Blazor okruhu — vrácené false by nic nezaručovalo.
            DemizonContext.DiscardPendingChange(member);
            logger.LogError(ex, "Failed to process Member operation.");
            return false;
        }
    }

    public async Task ConnectGoogleCalendarAsync(int memberId, string refreshToken, string calendarId)
    {
        var entity = await DemizonContext.Members.FindAsync(memberId)
            ?? throw new EntityNotFoundException($"Member with id: {memberId} not found.");

        entity.GoogleRefreshToken = refreshToken;
        entity.GoogleCalendarId = calendarId;
        entity.GoogleConnectedAt = DateTime.UtcNow;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task DisconnectGoogleCalendarAsync(int memberId)
    {
        var entity = await DemizonContext.Members.FindAsync(memberId)
            ?? throw new EntityNotFoundException($"Member with id: {memberId} not found.");

        entity.GoogleRefreshToken = null;
        entity.GoogleCalendarId = null;
        entity.GoogleConnectedAt = null;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // Entita je deklarovaná mimo try, aby po selhání šla vrátit do Unchanged.
        Dal.Entities.Member? entity = null;
        try
        {
            entity = await DemizonContext.Members.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }

            // Soft delete – data pro historii docházky zůstanou, global query filter skryje člena
            entity.DeletedAt = DateTime.UtcNow;
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Soft delete je z pohledu EF Modified — vrácením do Unchanged se zahodí,
            // aby se DeletedAt nenastavilo až při příštím nesouvisejícím uložení.
            DemizonContext.DiscardPendingChange(entity);
            logger.LogError(ex, "Failed to process Member operation.");
            return false;
        }
    }
}
