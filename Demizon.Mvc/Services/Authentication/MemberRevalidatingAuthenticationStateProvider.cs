using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Demizon.Mvc.Services.Authentication;

/// <summary>
/// A Blazor circuit keeps the user it was opened with and never sees the cookie again, so the cookie
/// check alone would leave an open page running with the old roles. This re-checks the circuit's user.
/// </summary>
public sealed class MemberRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        if (authenticationState.User.Identity?.IsAuthenticated != true)
            return true;

        // Own scope: the circuit's DbContext is shared with the page and is not thread-safe.
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MemberSessionValidator>()
            .IsCurrentAsync(authenticationState.User, cancellationToken);
    }
}
