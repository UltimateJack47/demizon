using Plugin.Firebase.CloudMessaging;

namespace Demizon.Maui.Services;

/// <summary>
/// Handles deep-link navigation from a tapped notification to the relevant
/// EventDetail page (own attendance edit). Covers three scenarios:
///   1. Cold start  — tap stored as pending; replayed after auto-login completes.
///   2. Background  — FCM NotificationTapped fires when app returns to foreground.
///   3. Foreground  — local notification tap arrives via MainActivity.OnNewIntent
///                    and is forwarded here by reading intent extras.
/// </summary>
public class NotificationNavigationService
{
    private readonly object _gate = new();
    private PendingTarget? _pending;
    private bool _ready;

    public void Initialize()
    {
        CrossFirebaseCloudMessaging.Current.NotificationTapped += (_, e) =>
            Handle(e.Notification.Data);
    }

    public void Handle(IDictionary<string, string>? data)
    {
        var target = Parse(data);
        if (target is null) return;

        bool navigateNow;
        lock (_gate)
        {
            if (_ready)
            {
                navigateNow = true;
            }
            else
            {
                _pending = target;
                navigateNow = false;
            }
        }

        if (navigateNow)
            DispatchNavigate(target);
    }

    /// <summary>
    /// Called after Shell has navigated to MainTabs post-login.
    /// Replays any pending tap that arrived before Shell was ready.
    /// </summary>
    public void MarkReadyAndFlush()
    {
        PendingTarget? pending;
        lock (_gate)
        {
            _ready = true;
            pending = _pending;
            _pending = null;
        }

        if (pending is not null)
            DispatchNavigate(pending);
    }

    /// <summary>Resets state on logout so stale targets don't fire after re-login.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _ready = false;
            _pending = null;
        }
    }

    private static PendingTarget? Parse(IDictionary<string, string>? data)
    {
        if (data is null) return null;

        if (data.TryGetValue("eventId", out var eventIdRaw) &&
            int.TryParse(eventIdRaw?.ToString(), out var eventId) && eventId > 0)
        {
            return new PendingTarget(eventId, null);
        }

        if (data.TryGetValue("rehearsalDate", out var dateRaw) &&
            !string.IsNullOrWhiteSpace(dateRaw))
        {
            return new PendingTarget(null, dateRaw);
        }

        return null;
    }

    private static void DispatchNavigate(PendingTarget target)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var route = target.EventId is int id
                    ? $"{AppRoutes.EventDetail}?eventId={id}"
                    : $"{AppRoutes.EventDetail}?rehearsalDate={target.RehearsalDate}";
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationNavigation] GoToAsync failed: {ex}");
            }
        });
    }

    private sealed record PendingTarget(int? EventId, string? RehearsalDate);
}
