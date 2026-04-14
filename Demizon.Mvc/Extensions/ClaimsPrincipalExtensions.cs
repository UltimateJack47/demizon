using System.Security.Claims;

namespace Demizon.Mvc.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetMemberId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirstValue(ClaimTypes.PrimarySid)
            ?? throw new InvalidOperationException("Member ID claim missing."));
}
