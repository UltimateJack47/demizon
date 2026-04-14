namespace Demizon.Maui.Services;

public class TokenStorage
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public async Task<string?> GetAccessTokenAsync()
        => await SecureStorage.Default.GetAsync(AccessTokenKey);

    public async Task<string?> GetRefreshTokenAsync()
        => await SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task SaveAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
    }
}
