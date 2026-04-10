using System.Security.Claims;
using Demizon.Common.Services;

namespace Demizon.Mvc.Services;

/// <summary>
/// Implementace ICurrentUserAccessor pro ASP.NET Core – čte login z HTTP kontextu.
/// </summary>
public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public string? GetCurrentUserLogin() =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);
}
