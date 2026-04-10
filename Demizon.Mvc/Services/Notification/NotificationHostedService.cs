using Demizon.Common.Configuration;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

namespace Demizon.Mvc.Services.Notification;

/// <summary>
/// Background service, která jednou denně kontroluje blížící se akce
/// a odesílá Web Push notifikace členům s aktivní subscripcí.
/// </summary>
public sealed class NotificationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<VapidSettings> vapidOptions,
    ILogger<NotificationHostedService> logger)
    : BackgroundService
{
    private readonly VapidSettings _vapid = vapidOptions.Value;

    // Kontrola probíhá každý den v 8:00
    private static readonly TimeOnly CheckTime = new(8, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationHostedService spuštěna.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextCheck();
                logger.LogInformation("Další kontrola notifikací za {Delay}.", delay);
                await Task.Delay(delay, stoppingToken);

                await CheckAndSendNotificationsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown – normální stav
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "NotificationHostedService selhala s nepředvídanou chybou.");
            throw;
        }
    }

    private async Task CheckAndSendNotificationsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<IPushSubscriptionService>();

        var today = DateTime.Today;

        // Najdeme akce, které mají nastavenou notifikaci a datum notifikace je dnes
        var upcomingEvents = eventService.GetAll()
            .AsNoTracking()
            .Where(e => e.NotifyBeforeDays.HasValue && e.DateFrom > today)
            .ToList()
            .Where(e => e.DateFrom.Date == today.AddDays(e.NotifyBeforeDays!.Value))
            .ToList();

        if (upcomingEvents.Count == 0)
        {
            logger.LogInformation("Žádné akce k notifikaci dnes ({Today}).", today.ToShortDateString());
            return;
        }

        var allSubscriptions = await subscriptionService.GetAllAsync();
        if (allSubscriptions.Count == 0) return;

        var pushClient = new WebPushClient();

        foreach (var evt in upcomingEvents)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title = $"Blíží se akce: {evt.Name}",
                body = $"{evt.DateFrom:d.M.yyyy} – {evt.Place ?? "místo TBD"}. Nezapomeň vyplnit docházku!",
                url = "/Admin/MemberAttendance/"
            });

            foreach (var sub in allSubscriptions)
            {
                try
                {
                    var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    var vapidDetails = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
                    await pushClient.SendNotificationAsync(subscription, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                                   || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Subscription vypršela – odstraníme ji z DB
                    logger.LogWarning("Subscription {Endpoint} je neplatná, odstraňuji.", sub.Endpoint);
                    await subscriptionService.RemoveAsync(sub.MemberId, sub.Endpoint);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Chyba při odesílání push notifikace na {Endpoint}.", sub.Endpoint);
                }
            }

            logger.LogInformation("Notifikace odeslána pro akci '{Name}' ({Count} odběratelů).",
                evt.Name, allSubscriptions.Count);
        }
    }

    private static TimeSpan GetDelayUntilNextCheck()
    {
        var now = DateTime.Now;
        var nextRun = DateTime.Today.Add(CheckTime.ToTimeSpan());
        if (now >= nextRun)
            nextRun = nextRun.AddDays(1);
        return nextRun - now;
    }
}
