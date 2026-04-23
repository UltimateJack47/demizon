using Demizon.Contracts.Notifications;
using Demizon.Core.Services.Notification;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services.Notification;

/// <summary>
/// Shared push dispatch for both the admin "notify missing attendance" endpoint
/// and <see cref="UnifiedNotificationService"/>. Owns the web push + FCM fan-out primitives
/// so there is a single place that knows how to reach a member (or everyone).
/// </summary>
public sealed class AttendanceReminderService(
    DemizonContext db,
    IPushSubscriptionService subscriptionService,
    WebPushSender webPush,
    FcmService fcm,
    ILogger<AttendanceReminderService> logger)
{
    /// <summary>
    /// Admin-triggered reminder for members who don't yet have an attendance record for the event.
    /// Records <see cref="NotificationType.EventManualReminder"/> per notified member so the scheduler
    /// skips them at the next milestone.
    /// </summary>
    public async Task<NotifyMissingAttendanceResult> NotifyMissingAttendanceAsync(
        int eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null) return NotifyMissingAttendanceResult.NotFound;
        if (ev.IsCancelled) return NotifyMissingAttendanceResult.Cancelled;
        if (ev.DateFrom <= DateTime.UtcNow) return NotifyMissingAttendanceResult.AlreadyPast;

        var membersWithAttendance = await db.Attendances
            .Where(a => a.EventId == ev.Id)
            .Select(a => a.MemberId)
            .ToListAsync(ct);

        var membersToNotify = await db.Members
            .Where(m => !membersWithAttendance.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (membersToNotify.Count == 0)
            return NotifyMissingAttendanceResult.Ok(
                new NotifyMissingAttendanceResponse(0, membersWithAttendance.Count));

        var title = "Doplň si docházku";
        var body = $"{ev.Name} – {ev.DateFrom:d.M.yyyy}{(ev.Place != null ? $" ({ev.Place})" : "")}";
        var data = new Dictionary<string, string> { ["eventId"] = ev.Id.ToString() };
        var now = DateTime.UtcNow;

        foreach (var memberId in membersToNotify)
        {
            await SendToMemberAsync(memberId, title, body, data, ct);

            db.SentNotifications.Add(new SentNotification
            {
                EventId = ev.Id,
                MemberId = memberId,
                NotificationType = NotificationType.EventManualReminder,
                SentAt = now,
            });
        }

        await db.SaveChangesAsync(ct);

        return NotifyMissingAttendanceResult.Ok(
            new NotifyMissingAttendanceResponse(membersToNotify.Count, membersWithAttendance.Count));
    }

    /// <summary>
    /// Web Push + FCM fan-out to a single member.
    /// </summary>
    public async Task SendToMemberAsync(
        int memberId, string title, string body,
        Dictionary<string, string>? data, CancellationToken ct = default)
    {
        var memberSubs = await subscriptionService.GetByMemberAsync(memberId);
        foreach (var sub in memberSubs)
            await webPush.SendAsync(sub, title, body);

        var deviceTokens = await db.DeviceTokens
            .Where(d => d.MemberId == memberId)
            .ToListAsync(ct);

        foreach (var dt in deviceTokens)
        {
            try { await fcm.SendAsync(dt.Token, title, body, data); }
            catch (Exception ex)
            {
                logger.LogError(ex, "FCM send failed for member {MemberId}, token {Token}.", memberId, dt.Token);
            }
        }
    }

    /// <summary>
    /// Web Push + FCM broadcast to every subscribed member / registered device.
    /// </summary>
    public async Task SendToAllMembersAsync(
        string title, string body,
        Dictionary<string, string>? data, CancellationToken ct = default)
    {
        var allSubscriptions = await subscriptionService.GetAllAsync();
        foreach (var sub in allSubscriptions)
            await webPush.SendAsync(sub, title, body);

        var allDeviceTokens = await db.DeviceTokens.ToListAsync(ct);
        foreach (var dt in allDeviceTokens)
        {
            try { await fcm.SendAsync(dt.Token, title, body, data); }
            catch (Exception ex)
            {
                logger.LogError(ex, "FCM send failed for token {Token}.", dt.Token);
            }
        }
    }
}

/// <summary>
/// Result of <see cref="AttendanceReminderService.NotifyMissingAttendanceAsync"/>.
/// </summary>
public readonly record struct NotifyMissingAttendanceResult(
    NotifyMissingAttendanceOutcome Outcome,
    NotifyMissingAttendanceResponse? Response)
{
    public static NotifyMissingAttendanceResult NotFound { get; } = new(NotifyMissingAttendanceOutcome.NotFound, null);
    public static NotifyMissingAttendanceResult Cancelled { get; } = new(NotifyMissingAttendanceOutcome.Cancelled, null);
    public static NotifyMissingAttendanceResult AlreadyPast { get; } = new(NotifyMissingAttendanceOutcome.AlreadyPast, null);
    public static NotifyMissingAttendanceResult Ok(NotifyMissingAttendanceResponse response) =>
        new(NotifyMissingAttendanceOutcome.Ok, response);
}

public enum NotifyMissingAttendanceOutcome
{
    Ok,
    NotFound,
    Cancelled,
    AlreadyPast,
}
