using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Notifications;
using Demizon.Maui.Services;

namespace Demizon.Maui.ViewModels;

public partial class ProfileViewModel(IApiClient apiClient, TokenStorage tokenStorage) : ObservableObject
{
    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private bool _isGoogleCalendarConnected;

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private bool _isBusy;

    public string AppVersion => AppInfo.Current.VersionString;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var token = await tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) return;

        try
        {
            var claims = ParseJwtClaims(token);
            Login = GetClaim(claims, "sub") ?? GetClaim(claims, "unique_name") ?? "—";
            Role = GetClaim(claims, "role") ?? "—";

            var gcalClaim = GetClaim(claims, "gcal_connected");
            IsGoogleCalendarConnected = string.Equals(gcalClaim, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            Login = "—";
            Role = "—";
        }
    }

    [RelayCommand]
    private async Task ToggleNotificationsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // Placeholder FCM token – actual registration requires Firebase setup
            const string placeholderToken = "placeholder-fcm-token";
            var request = new RegisterDeviceRequest(placeholderToken, "android");

            if (NotificationsEnabled)
                await apiClient.RegisterDeviceAsync(request);
            else
                await apiClient.UnregisterDeviceAsync(request);
        }
        catch
        {
            // Revert on failure
            NotificationsEnabled = !NotificationsEnabled;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        tokenStorage.Clear();
        await Shell.Current.GoToAsync("//login");
    }

    private static Dictionary<string, JsonElement>? ParseJwtClaims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        var payload = parts[1];
        // Base64url → standard base64
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var bytes = Convert.FromBase64String(payload);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
    }

    private static string? GetClaim(Dictionary<string, JsonElement>? claims, string key)
    {
        if (claims is null || !claims.TryGetValue(key, out var value)) return null;
        return value.ToString();
    }
}
