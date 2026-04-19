using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demizon.Contracts.Notifications;
using Demizon.Maui.Services;
using Plugin.Firebase.CloudMessaging;

namespace Demizon.Maui.ViewModels;

public partial class ProfileViewModel(IApiClient apiClient, TokenStorage tokenStorage, INavigationService navigation) : ObservableObject
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

    [ObservableProperty]
    private string? _notificationError;

    [ObservableProperty]
    private string? _testNotificationMessage;

    private bool _handlingNotificationToggle;

    public string AppVersion => AppInfo.Current.VersionString;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var login = await tokenStorage.GetLoginAsync();
        var role = await tokenStorage.GetRoleAsync();
        Login = login ?? "—";
        Role = role ?? "—";

        // Restore saved notification preference without triggering the toggle handler
        _handlingNotificationToggle = true;
        NotificationsEnabled = Preferences.Default.Get("notifications_enabled", false);
        _handlingNotificationToggle = false;

        // If notifications were previously enabled, re-register the FCM token on each load
        // (token may have changed or the previous registration used a placeholder)
        if (NotificationsEnabled)
            MainThread.BeginInvokeOnMainThread(async () => await EnsureTokenRegisteredAsync());

        var token = await tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            IsGoogleCalendarConnected = await tokenStorage.GetIsGoogleCalendarConnectedAsync();
        }
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        if (_handlingNotificationToggle) return;
        MainThread.BeginInvokeOnMainThread(async () => await HandleNotificationToggleAsync(value));
    }

    private async Task HandleNotificationToggleAsync(bool enable)
    {
        if (_handlingNotificationToggle) return;
        _handlingNotificationToggle = true;
        IsBusy = true;
        NotificationError = null;

        try
        {
            if (enable)
            {
                var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    NotificationError = "Oprávnění k notifikacím zamítnuto.";
                    NotificationsEnabled = false;
                    return;
                }
            }

            string? fcmToken;
            try
            {
                fcmToken = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            }
            catch (Exception ex)
            {
                NotificationError = $"FCM chyba: {ex.Message}";
                NotificationsEnabled = !enable;
                return;
            }

            if (string.IsNullOrEmpty(fcmToken))
            {
                NotificationError = "FCM token nelze získat — zkontrolujte google-services.json a ApplicationId.";
                NotificationsEnabled = !enable;
                return;
            }

            var request = new RegisterDeviceRequest(fcmToken, "android");

            if (enable)
                await apiClient.RegisterDeviceAsync(request);
            else
                await apiClient.UnregisterDeviceAsync(request);

            Preferences.Default.Set("notifications_enabled", enable);
        }
        catch (Exception ex)
        {
            NotificationError = $"Chyba API: {ex.Message}";
            NotificationsEnabled = !enable;
        }
        finally
        {
            _handlingNotificationToggle = false;
            IsBusy = false;
        }
    }

    private async Task EnsureTokenRegisteredAsync()
    {
        try
        {
            var fcmToken = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (string.IsNullOrEmpty(fcmToken))
            {
                NotificationError = "Re-registrace selhala: FCM token prázdný.";
                return;
            }
            await apiClient.RegisterDeviceAsync(new RegisterDeviceRequest(fcmToken, "android"));
        }
        catch (Exception ex)
        {
            NotificationError = $"Re-registrace selhala: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SendTestNotificationAsync()
    {
        TestNotificationMessage = null;
        try
        {
            await apiClient.SendTestNotificationAsync();
            TestNotificationMessage = "✓ Notifikace odeslána";
        }
        catch (Exception ex)
        {
            TestNotificationMessage = ex.Message.Contains("503") || ex.Message.Contains("Firebase")
                ? "✗ Firebase není nakonfigurován na serveru"
                : "✗ Žádné registrované zařízení — nejdřív povol notifikace";
        }
    }

    [RelayCommand]
    private async Task EditProfileAsync()
    {
        await navigation.GoToAsync(AppRoutes.EditProfile);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        tokenStorage.Clear();
        await navigation.GoToAsync(AppRoutes.Login);
    }

}
