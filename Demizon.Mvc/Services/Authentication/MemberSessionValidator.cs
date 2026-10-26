using System.Security.Claims;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services.Authentication;

/// <summary>
/// Decides whether a signed-in session still describes the member as the database does.
/// Roles are baked into the cookie (30 days, sliding) and the JWT, so without this check a removed
/// role — or a deleted member — would keep working until the session expired.
/// </summary>
public sealed class MemberSessionValidator(DemizonContext db)
{
    public async Task<bool> IsCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(principal.FindFirst(ClaimTypes.PrimarySid)?.Value, out var memberId))
            return false;

        // The global filter hides soft-deleted members, so a deleted member reads as null.
        var member = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => new { m.IsExternal, Roles = m.Roles.Select(r => r.Role).ToList() })
            .SingleOrDefaultAsync(cancellationToken);
        if (member is null || member.IsExternal)
            return false;

        var issuedRoles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        return issuedRoles.SetEquals(member.Roles.Select(r => r.ToString()));
    }
}
