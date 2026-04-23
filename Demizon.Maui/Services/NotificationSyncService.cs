using Plugin.Firebase.CloudMessaging;
using Demizon.Contracts.Notifications;

namespace Demizon.Maui.Services;

public class NotificationSyncService(IApiClient apiClient)
{
    public async Task SyncAsync()
    {
        try
        {
            Console.WriteLine("[DEBUG_LOG] NotificationSyncService: Starting sync...");
            
            var savedPref = Preferences.Default.Get("notifications_enabled", false);
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            var actuallyEnabled = savedPref && status == PermissionStatus.Granted;

            Console.WriteLine($"[DEBUG_LOG] NotificationSyncService: savedPref={savedPref}, systemStatus={status}, actuallyEnabled={actuallyEnabled}");

            if (savedPref && !actuallyEnabled)
            {
                Console.WriteLine("[DEBUG_LOG] NotificationSyncService: Mismatch detected. Revoking token on server.");
                var fcmToken = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
                if (!string.IsNullOrEmpty(fcmToken))
                {
                    await apiClient.UnregisterDeviceAsync(new RegisterDeviceRequest(fcmToken, "android"));
                    Console.WriteLine("[DEBUG_LOG] NotificationSyncService: Token unregistered.");
                }
                Preferences.Default.Set("notifications_enabled", false);
            }
            else if (actuallyEnabled)
            {
                Console.WriteLine("[DEBUG_LOG] NotificationSyncService: Notifications enabled. Refreshing registration.");
                var fcmToken = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
                if (!string.IsNullOrEmpty(fcmToken))
                {
                    await apiClient.RegisterDeviceAsync(new RegisterDeviceRequest(fcmToken, "android"));
                    Console.WriteLine("[DEBUG_LOG] NotificationSyncService: Token registered/refreshed.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG_LOG] NotificationSyncService error: {ex.Message}");
        }
    }
}
