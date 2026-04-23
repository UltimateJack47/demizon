using Demizon.Common.Configuration;
using Demizon.Core.Services.Notification;
using Microsoft.Extensions.Options;
using WebPush;

namespace Demizon.Mvc.Services.Notification;

/// <summary>
/// Thin helper for sending a single web push payload. Handles dead-subscription cleanup.
/// Used by controllers that need to fire push notifications on demand (e.g., admin manual reminders).
/// </summary>
public sealed class WebPushSender(
    IOptions<VapidSettings> vapidOptions,
    IPushSubscriptionService subscriptionService,
    ILogger<WebPushSender> logger)
{
    private readonly VapidSettings _vapid = vapidOptions.Value;
    private readonly WebPushClient _client = new();

    public async Task SendAsync(
        Dal.Entities.PushSubscription sub, string title, string body, string? url = null)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title,
                body,
                url = url ?? "/Admin/MemberAttendance/"
            });

            var subscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            var vapidDetails = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
            await _client.SendNotificationAsync(subscription, payload, vapidDetails);
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
