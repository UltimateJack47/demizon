using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.View;
using Demizon.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
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
[IntentFilter(new[] { "OPEN_EVENT_DETAIL" }, Categories = new[] { Intent.CategoryDefault })]
public class MainActivity : MauiAppCompatActivity
{
    internal const string ChannelId = "demizon_channel";
    private const string DeepLinkFlagExtra = "demizon_deep_link";

    /// <summary>
    /// Page-scoped horizontal-swipe handler. Pages set this in OnAppearing
    /// and clear it in OnDisappearing. Only one at a time — deeper shell
    /// navigation wouldn't see concurrent pages.
    /// </summary>
    internal static SwipeGestureInterceptor? CurrentSwipeInterceptor { get; set; }

    public override bool DispatchTouchEvent(global::Android.Views.MotionEvent? e)
    {
        var interceptor = CurrentSwipeInterceptor;
        if (interceptor is not null && e is not null && interceptor.OnDispatchTouch(e))
            return true;
        return base.DispatchTouchEvent(e);
    }

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
        DispatchLocalDeepLinkIfAny(Intent);

        // Show FCM notifications that arrive while the app is in the foreground
        CrossFirebaseCloudMessaging.Current.NotificationReceived += (sender, e) =>
        {
            System.Console.WriteLine($"[DEBUG_LOG] NotificationReceived: {e.Notification.Title}");
            ShowLocalNotification(e.Notification.Title ?? "Demižón", e.Notification.Body ?? string.Empty, e.Notification.Data);
        };
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

    internal void ShowLocalNotification(string title, string body, IDictionary<string, string>? data)
    {
        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        intent.PutExtra(DeepLinkFlagExtra, true);

        if (data != null)
        {
            foreach (var key in data.Keys)
                intent.PutExtra(key, data[key]);
        }

        var requestCode = System.Environment.TickCount;
        var pendingIntent = PendingIntent.GetActivity(
            this,
            requestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .SetPriority(NotificationCompat.PriorityDefault)
            .Build()!;

        NotificationManagerCompat.From(this)!.Notify(System.Environment.TickCount, notification);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        FirebaseCloudMessagingImplementation.OnNewIntent(intent);
        DispatchLocalDeepLinkIfAny(intent);
    }

    /// <summary>
    /// Foreground-tap path: local notifications rendered by ShowLocalNotification
    /// reach us here via Intent extras (they don't go through FCM NotificationTapped).
    /// We flag them with <see cref="DeepLinkFlagExtra"/> so we ignore unrelated intents.
    /// </summary>
    private static void DispatchLocalDeepLinkIfAny(Intent? intent)
    {
        if (intent is null) return;
        if (!intent.GetBooleanExtra(DeepLinkFlagExtra, false)) return;

        var data = new Dictionary<string, string>();
        var eventId = intent.GetStringExtra("eventId");
        if (!string.IsNullOrEmpty(eventId)) data["eventId"] = eventId;
        var rehearsalDate = intent.GetStringExtra("rehearsalDate");
        if (!string.IsNullOrEmpty(rehearsalDate)) data["rehearsalDate"] = rehearsalDate;

        if (data.Count == 0) return;

        // Clear the flag so a subsequent config change / resume doesn't re-trigger navigation.
        intent.RemoveExtra(DeepLinkFlagExtra);

        var nav = IPlatformApplication.Current?.Services.GetService<NotificationNavigationService>();
        nav?.Handle(data);
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
