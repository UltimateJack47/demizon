using Plugin.Firebase.CloudMessaging;

namespace Demizon.Maui.Services;

public class NotificationNavigationService
{
    public void Initialize()
    {
        CrossFirebaseCloudMessaging.Current.NotificationTapped += (sender, e) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (e.Notification.Data.TryGetValue("eventId", out var eventIdObj) && int.TryParse(eventIdObj.ToString(), out var eventId))
                {
                    await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?eventId={eventId}");
                }
                else if (e.Notification.Data.TryGetValue("rehearsalDate", out var dateObj))
                {
                    await Shell.Current.GoToAsync($"{AppRoutes.EventDetail}?rehearsalDate={dateObj}");
                }
            });
        };
    }
}
