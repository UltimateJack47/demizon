using Demizon.Common.Configuration;
using Demizon.Core.Services.Notification;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace Demizon.Mvc.Services.Notification;

/// <summary>
/// Unified background service that handles all notification logic:
/// - Rehearsal attendance reminders (5d, 3d, 1d before Friday)
/// - Event reminders with intelligent milestone timing (1h after creation, 60d, 30d, 14d before event)
/// Sends both Web Push and FCM notifications. Deduplication via SentNotification table.
/// </summary>
public sealed class UnifiedNotificationService(
    IServiceScopeFactory scopeFactory,
    IOptions<VapidSettings> vapidOptions,
    ILogger<UnifiedNotificationService> logger)
    : BackgroundService
{
    private readonly VapidSettings _vapid = vapidOptions.Value;

    // Event milestones in days before event
    private static readonly (int Days, NotificationType Type)[] EventMilestones =
    [
        (60, NotificationType.EventReminder60Days),
        (30, NotificationType.EventReminder30Days),
        (14, NotificationType.EventReminder14Days),
    ];

    // Rehearsal milestones in days before rehearsal
    private static readonly (int Days, NotificationType Type)[] RehearsalMilestones =
    [
        (5, NotificationType.RehearsalReminder5Days),
        (3, NotificationType.RehearsalReminder3Days),
        (1, NotificationType.RehearsalReminder1Day),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("UnifiedNotificationService started.");

        // Small initial delay to let app fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        // Run immediately on startup, then every hour
        await RunCheckAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCheckAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in UnifiedNotificationService check cycle.");
            }
        }
    }

    private async Task RunCheckAsync(CancellationToken ct)
    {
        logger.LogInformation("Running notification check at {Time}.", DateTime.UtcNow);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<IPushSubscriptionService>();
        var fcm = scope.ServiceProvider.GetRequiredService<FcmService>();

        var now = DateTime.UtcNow;

        await CheckNewEventNotificationsAsync(db, subscriptionService, fcm, now, ct);
        await CheckEventMilestoneNotificationsAsync(db, subscriptionService, fcm, now, ct);
        await CheckRehearsalRemindersAsync(db, subscriptionService, fcm, now, ct);
    }

    /// <summary>
    /// Send "new event" notification 1 hour after event creation.
    /// </summary>
    private async Task CheckNewEventNotificationsAsync(
        DemizonContext db, IPushSubscriptionService subscriptionService,
        FcmService fcm, DateTime now, CancellationToken ct)
    {
        // Find events created more than 1 hour ago that haven't been notified yet
        var cutoff = now.AddHours(-1);
        var events = await db.Events
            .Where(e => !e.IsCancelled && e.DateFrom > now && e.CreatedAt <= cutoff)
            .ToListAsync(ct);

        foreach (var ev in events)
        {
            // Check if already sent
            var alreadySent = await db.SentNotifications
                .AnyAsync(n => n.EventId == ev.Id && n.NotificationType == NotificationType.NewEvent && n.MemberId == null, ct);

            if (alreadySent) continue;

            var title = "Nová akce";
            var body = $"{ev.Name} – {ev.DateFrom:d.M.yyyy}{(ev.Place != null ? $" ({ev.Place})" : "")}";

            await SendToAllMembersAsync(db, subscriptionService, fcm, title, body,
                new Dictionary<string, string> { ["eventId"] = ev.Id.ToString() }, ct);

            // Record as broadcast (MemberId = null)
            db.SentNotifications.Add(new SentNotification
            {
                EventId = ev.Id,
                NotificationType = NotificationType.NewEvent,
                SentAt = now,
                MemberId = null,
            });
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Sent 'new event' notification for '{Name}'.", ev.Name);
        }
    }

    /// <summary>
    /// Intelligent milestone-based event reminders (60d, 30d, 14d).
    /// Skips milestones that already passed at event creation time.
    /// </summary>
    private async Task CheckEventMilestoneNotificationsAsync(
        DemizonContext db, IPushSubscriptionService subscriptionService,
        FcmService fcm, DateTime now, CancellationToken ct)
    {
        var futureEvents = await db.Events
            .Where(e => !e.IsCancelled && e.DateFrom > now)
            .ToListAsync(ct);

        foreach (var ev in futureEvents)
        {
            var daysUntilEvent = (ev.DateFrom.Date - now.Date).TotalDays;

            foreach (var (milestoneDays, notifType) in EventMilestones)
            {
                // Only trigger if we're within the milestone window
                if (daysUntilEvent > milestoneDays) continue;

                // Find the next smaller milestone to avoid duplicate-range notifications
                var nextSmallerMilestone = EventMilestones
                    .Where(m => m.Days < milestoneDays)
                    .Select(m => (int?)m.Days)
                    .Max() ?? 0;

                if (daysUntilEvent <= nextSmallerMilestone) continue;

                // Intelligent skip: if the event was created when this milestone had already passed, skip it
                var daysFromCreationToEvent = (ev.DateFrom.Date - ev.CreatedAt.Date).TotalDays;
                if (daysFromCreationToEvent <= milestoneDays)
                {
                    // This milestone was already irrelevant at creation time
                    // But only skip if there's a larger milestone that was also skipped
                    // (i.e., the event was created within this milestone window)
                    // We still want to send if this is the first applicable milestone
                    var largerMilestones = EventMilestones.Where(m => m.Days > milestoneDays).ToList();
                    if (largerMilestones.Count > 0 && daysFromCreationToEvent <= largerMilestones.Min(m => m.Days))
                    {
                        // All larger milestones were also irrelevant, skip this one only if
                        // the "new event" notification effectively covered it
                        continue;
                    }
                }

                // Check if already sent
                var alreadySent = await db.SentNotifications
                    .AnyAsync(n => n.EventId == ev.Id && n.NotificationType == notifType && n.MemberId == null, ct);

                if (alreadySent) continue;

                var daysText = milestoneDays switch
                {
                    >= 60 => "2 měsíce",
                    >= 30 => "měsíc",
                    >= 14 => "2 týdny",
                    _ => $"{milestoneDays} dní"
                };

                var title = $"Připomínka akce za {daysText}";
                var body = $"{ev.Name} – {ev.DateFrom:d.M.yyyy}{(ev.Place != null ? $" ({ev.Place})" : "")}";

                await SendToAllMembersAsync(db, subscriptionService, fcm, title, body,
                    new Dictionary<string, string> { ["eventId"] = ev.Id.ToString() }, ct);

                db.SentNotifications.Add(new SentNotification
                {
                    EventId = ev.Id,
                    NotificationType = notifType,
                    SentAt = now,
                    MemberId = null,
                });
                await db.SaveChangesAsync(ct);

                logger.LogInformation("Sent '{Type}' notification for '{Name}'.", notifType, ev.Name);
            }
        }
    }

    /// <summary>
    /// Rehearsal reminders for members who haven't filled in attendance.
    /// Checks 5d, 3d, 1d before each Friday rehearsal.
    /// </summary>
    private async Task CheckRehearsalRemindersAsync(
        DemizonContext db, IPushSubscriptionService subscriptionService,
        FcmService fcm, DateTime now, CancellationToken ct)
    {
        // Find upcoming Fridays within the next 7 days
        var upcomingFridays = Enumerable.Range(0, 7)
            .Select(d => now.Date.AddDays(d))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday)
            .ToList();

        // Exclude Fridays that have events (those are not rehearsals)
        var eventDates = await db.Events
            .Where(e => upcomingFridays.Contains(e.DateFrom.Date))
            .Select(e => e.DateFrom.Date)
            .ToListAsync(ct);

        var rehearsalFridays = upcomingFridays.Where(f => !eventDates.Contains(f)).ToList();

        foreach (var friday in rehearsalFridays)
        {
            var daysUntil = (friday - now.Date).TotalDays;

            foreach (var (milestoneDays, notifType) in RehearsalMilestones)
            {
                // Check if we're at this milestone (within 1-day window for hourly checks)
                if (daysUntil > milestoneDays || daysUntil < milestoneDays - 1) continue;

                // Find members without attendance for this rehearsal
                var membersWithAttendance = await db.Attendances
                    .Where(a => a.EventId == null && a.Date == friday)
                    .Select(a => a.MemberId)
                    .ToListAsync(ct);

                var membersToNotify = await db.Members
                    .Where(m => !membersWithAttendance.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                foreach (var memberId in membersToNotify)
                {
                    // Check if already sent for this member + rehearsal + type
                    var alreadySent = await db.SentNotifications
                        .AnyAsync(n => n.MemberId == memberId
                            && n.RehearsalDate == friday
                            && n.NotificationType == notifType, ct);

                    if (alreadySent) continue;

                    var title = "Nevyplněná docházka na zkoušku";
                    var body = $"Zkouška {friday:d.M.yyyy} – nezapomeň vyplnit docházku!";

                    await SendToMemberAsync(db, subscriptionService, fcm, memberId, title, body, null, ct);

                    db.SentNotifications.Add(new SentNotification
                    {
                        MemberId = memberId,
                        RehearsalDate = friday,
                        NotificationType = notifType,
                        SentAt = now,
                    });
                }

                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Send notification to all members via both Web Push and FCM.
    /// </summary>
    private async Task SendToAllMembersAsync(
        DemizonContext db, IPushSubscriptionService subscriptionService,
        FcmService fcm, string title, string body,
        Dictionary<string, string>? data, CancellationToken ct)
    {
        // Web Push
        await SendWebPushToAllAsync(subscriptionService, title, body, ct);

        // FCM
        var allDeviceTokens = await db.DeviceTokens.ToListAsync(ct);
        foreach (var dt in allDeviceTokens)
        {
            await fcm.SendAsync(dt.Token, title, body, data);
        }
    }

    /// <summary>
    /// Send notification to a specific member via both Web Push and FCM.
    /// </summary>
    private async Task SendToMemberAsync(
        DemizonContext db, IPushSubscriptionService subscriptionService,
        FcmService fcm, int memberId, string title, string body,
        Dictionary<string, string>? data, CancellationToken ct)
    {
        // Web Push
        var memberSubs = await subscriptionService.GetByMemberAsync(memberId);
        foreach (var sub in memberSubs)
        {
            await SendWebPushAsync(subscriptionService, sub, title, body);
        }

        // FCM
        var deviceTokens = await db.DeviceTokens
            .Where(d => d.MemberId == memberId)
            .ToListAsync(ct);

        foreach (var dt in deviceTokens)
        {
            await fcm.SendAsync(dt.Token, title, body, data);
        }
    }

    private async Task SendWebPushToAllAsync(
        IPushSubscriptionService subscriptionService, string title, string body, CancellationToken ct)
    {
        var allSubscriptions = await subscriptionService.GetAllAsync();
        foreach (var sub in allSubscriptions)
        {
            await SendWebPushAsync(subscriptionService, sub, title, body);
        }
    }

    private async Task SendWebPushAsync(
        IPushSubscriptionService subscriptionService,
        Dal.Entities.PushSubscription sub, string title, string body)
    {
        try
        {
            var pushClient = new WebPushClient();
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title,
                body,
                url = "/Admin/MemberAttendance/"
            });

            var subscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            var vapidDetails = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
            await pushClient.SendNotificationAsync(subscription, payload, vapidDetails);
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                           || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("Subscription {Endpoint} expired, removing.", sub.Endpoint);
            await subscriptionService.RemoveAsync(sub.MemberId, sub.Endpoint);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending web push to {Endpoint}.", sub.Endpoint);
        }
    }
}
