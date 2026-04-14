using System.Security.Claims;
using Demizon.Common.Services;

namespace Demizon.Api.Services;

public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public string? GetCurrentUserLogin() =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
}
