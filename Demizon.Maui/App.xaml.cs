using System.Text;
using System.Text.Json;
using Demizon.Contracts.Auth;
using Demizon.Maui.Services;

namespace Demizon.Maui;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly TokenStorage _tokenStorage;

    public App(AppShell shell, TokenStorage tokenStorage)
    {
        InitializeComponent();
        _shell = shell;
        _tokenStorage = tokenStorage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);

        window.Created += (_, _) =>
        {
            Dispatcher.DispatchAsync(() => TryAutoLoginAsync());
        };

        return window;
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            await _tokenStorage.InitializeAsync();

            if (_tokenStorage.IsTokenValid())
            {
                await Shell.Current.GoToAsync("//events");
                return;
            }

            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (refreshToken is null) return;

            var login = await _tokenStorage.GetLoginAsync();
            var result = await RefreshTokenDirectAsync(refreshToken);

            if (result is not null)
            {
                await _tokenStorage.SaveAsync(result, login ?? string.Empty);
                await Shell.Current.GoToAsync("//events");
            }
            else
            {
                _tokenStorage.Clear();
            }
        }
        catch
        {
            _tokenStorage.Clear();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task<TokenResponse?> RefreshTokenDirectAsync(string refreshToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(ApiConfig.BaseUrl) };
        var payload = JsonSerializer.Serialize(new RefreshRequest(refreshToken), JsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/refresh", content);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);
    }
}
