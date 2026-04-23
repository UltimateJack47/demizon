using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.View;
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.CloudMessaging.Platforms.Android;
using Plugin.Firebase.Core.Platforms.Android;

namespace Demizon.Maui.Platforms.Android;

[Activity(
    Theme = "@style/Maui.MainTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    internal const string ChannelId = "demizon_channel";

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Android 15 (SDK 35+) enforces edge-to-edge — system bars draw over the app window.
        // Apply the system-bar insets as padding on the root content view so pages don't
        // render under the status bar (top) or gesture/nav bar (bottom).
        ApplySystemBarsInsets();

        CrossFirebase.Initialize(this);
        FirebaseCloudMessagingImplementation.OnNewIntent(Intent);
        EnsureNotificationChannel();

        // Show FCM notifications that arrive while the app is in the foreground
        CrossFirebaseCloudMessaging.Current.NotificationReceived += (_, e) =>
            ShowLocalNotification(e.Notification.Title ?? "Demižón", e.Notification.Body ?? string.Empty);
    }

    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var channel = new NotificationChannel(ChannelId, "Demižón", NotificationImportance.Default)
        {
            Description = "Připomínky a oznámení Demižón"
        };
        ((NotificationManager)GetSystemService(NotificationService)!).CreateNotificationChannel(channel);
    }

    internal void ShowLocalNotification(string title, string body)
    {
        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault)
            .Build()!;

        NotificationManagerCompat.From(this)!.Notify(System.Environment.TickCount, notification);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        FirebaseCloudMessagingImplementation.OnNewIntent(intent);
    }

    private void ApplySystemBarsInsets()
    {
        var content = Window?.DecorView.FindViewById(global::Android.Resource.Id.Content);
        if (content is null) return;

        ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsInsetsListener());
    }

    private sealed class SystemBarsInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(global::Android.Views.View v, WindowInsetsCompat insets)
        {
            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
            v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            return WindowInsetsCompat.Consumed;
        }
    }
}
