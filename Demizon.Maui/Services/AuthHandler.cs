using System.Net;
using System.Net.Http.Headers;
using Demizon.Contracts.Auth;

namespace Demizon.Maui.Services;

public class AuthHandler(TokenStorage tokenStorage, IApiClient apiClient) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenStorage.GetAccessTokenAsync();
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await TryRefreshAsync(cancellationToken);
            if (refreshed is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }

    private async Task<string?> TryRefreshAsync(CancellationToken ct)
    {
        var refreshToken = await tokenStorage.GetRefreshTokenAsync();
        if (refreshToken is null) return null;

        try
        {
            var result = await apiClient.RefreshAsync(new RefreshRequest(refreshToken));
            await tokenStorage.SaveAsync(result.Token, result.RefreshToken);
            return result.Token;
        }
        catch
        {
            tokenStorage.Clear();
            return null;
        }
    }
}
