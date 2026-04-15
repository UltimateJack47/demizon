using System.Net;
using System.Net.Http.Headers;

namespace Demizon.Maui.Services;

public class AuthHandler(TokenStorage tokenStorage) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshSemaphore = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Proactive refresh if token is about to expire
        if (!tokenStorage.IsTokenValid())
        {
            await TryRefreshAsync(cancellationToken);
        }

        var token = await tokenStorage.GetAccessTokenAsync();
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        // 401 fallback safety net
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
        await RefreshSemaphore.WaitAsync(ct);
        try
        {
            // Re-check after acquiring lock — another thread may have refreshed already
            if (tokenStorage.IsTokenValid())
                return await tokenStorage.GetAccessTokenAsync();

            var refreshToken = await tokenStorage.GetRefreshTokenAsync();
            if (refreshToken is null) return null;

            var login = await tokenStorage.GetLoginAsync();

            var result = await TokenRefreshHelper.RefreshAsync(refreshToken, InnerHandler!, ct);

            if (result is null)
            {
                tokenStorage.Clear();
                NavigateToLogin();
                return null;
            }

            await tokenStorage.SaveAsync(result, login ?? string.Empty);
            return result.Token;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            tokenStorage.Clear();
            NavigateToLogin();
            return null;
        }
        finally
        {
            RefreshSemaphore.Release();
        }
    }

    private static void NavigateToLogin()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current is not null)
                await Shell.Current.GoToAsync(AppRoutes.Login);
        });
    }
}
