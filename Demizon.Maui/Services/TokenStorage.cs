using Demizon.Contracts.Auth;

namespace Demizon.Maui.Services;

public class TokenStorage
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string ExpiresAtKey = "expires_at";
    private const string RoleKey = "role";
    private const string LoginKey = "login";

    // In-memory cache to avoid blocking async calls from UI thread
    private DateTime? _cachedExpiresAt;
    private bool _hasAccessToken;

    public async Task<string?> GetAccessTokenAsync()
        => await SecureStorage.Default.GetAsync(AccessTokenKey);

    public async Task<string?> GetRefreshTokenAsync()
        => await SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task<string?> GetLoginAsync()
        => await SecureStorage.Default.GetAsync(LoginKey);

    public async Task<string?> GetRoleAsync()
        => await SecureStorage.Default.GetAsync(RoleKey);

    public bool IsTokenValid()
    {
        return _hasAccessToken
               && _cachedExpiresAt.HasValue
               && _cachedExpiresAt.Value > DateTime.UtcNow.AddMinutes(5);
    }

    public async Task SaveAsync(TokenResponse response, string login)
    {
        var expiresAt = DateTime.UtcNow.AddSeconds(response.ExpiresIn);

        await SecureStorage.Default.SetAsync(AccessTokenKey, response.Token);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, response.RefreshToken);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, expiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync(RoleKey, response.Role);
        await SecureStorage.Default.SetAsync(LoginKey, login);

        _cachedExpiresAt = expiresAt;
        _hasAccessToken = true;
    }

    /// <summary>
    /// Restores in-memory cache from SecureStorage. Call once at app startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        var token = await SecureStorage.Default.GetAsync(AccessTokenKey);
        var expiresAtStr = await SecureStorage.Default.GetAsync(ExpiresAtKey);

        _hasAccessToken = token is not null;
        _cachedExpiresAt = expiresAtStr is not null
            && DateTime.TryParse(expiresAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : null;
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        SecureStorage.Default.Remove(RoleKey);
        SecureStorage.Default.Remove(LoginKey);

        _cachedExpiresAt = null;
        _hasAccessToken = false;
    }
}
