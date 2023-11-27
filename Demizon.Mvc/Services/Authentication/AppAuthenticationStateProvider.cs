using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Demizon.Mvc.Services.Authentication;

public class AppAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedSessionStorage SessionStorage;

    private const string SessionKey = "UserSession";

    private readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public AppAuthenticationStateProvider(ProtectedSessionStorage sessionStorage)
    {
        SessionStorage = sessionStorage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userSessionStorageResult = await SessionStorage.GetAsync<UserSession>(SessionKey);
            var userSession = userSessionStorageResult.Success ? userSessionStorageResult.Value : null;

            if (userSession is null)
            {
                return await Task.FromResult(new AuthenticationState(Anonymous));
            }

            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new(ClaimTypes.Name, userSession.UserName),
                new(ClaimTypes.Role, userSession.Role)
            }, "Basic"));
            return await Task.FromResult(new AuthenticationState(claimsPrincipal));
        }
        catch
        {
            return await Task.FromResult(new AuthenticationState(Anonymous));
        }
    }

    public async Task UpdateAuthenticationState(UserSession? userSession)
    {
        ClaimsPrincipal claimsPrincipal;
        if (userSession is not null)
        {
            await SessionStorage.SetAsync(SessionKey, userSession);
            claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new(ClaimTypes.Name, userSession.UserName),
                new(ClaimTypes.Role, userSession.Role),
                new(ClaimTypes.Authentication, "Basic")                
            }, "Basic"));
        }
        else
        {
            await SessionStorage.DeleteAsync(SessionKey);
            claimsPrincipal = Anonymous;
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
    }
}
