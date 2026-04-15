using System.Text;
using System.Text.Json;
using Demizon.Contracts.Auth;

namespace Demizon.Maui.Services;

/// <summary>
/// Sdílená logika pro refresh tokenu – používá se v App.xaml.cs (auto-login)
/// i v AuthHandler (401 fallback). Vždy využívá ApiConfig.BaseUrl.
/// </summary>
public static class TokenRefreshHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Provede refresh přes nový HttpClient (bez auth headeru).
    /// Vrátí null při jakékoliv chybě.
    /// </summary>
    public static async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        using var client = new HttpClient { BaseAddress = new Uri(ApiConfig.BaseUrl) };
        var payload = JsonSerializer.Serialize(new RefreshRequest(refreshToken), JsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/refresh", content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);
    }

    /// <summary>
    /// Overload pro AuthHandler – používá InnerHandler aby se vyhnul cyklické DI závislosti.
    /// </summary>
    public static async Task<TokenResponse?> RefreshAsync(
        string refreshToken, HttpMessageHandler innerHandler, CancellationToken ct = default)
    {
        using var client = new HttpClient(innerHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        };
        var payload = JsonSerializer.Serialize(new RefreshRequest(refreshToken), JsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/refresh", content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);
    }
}
