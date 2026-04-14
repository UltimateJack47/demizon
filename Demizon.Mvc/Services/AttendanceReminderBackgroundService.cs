using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services;

public class AttendanceReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AttendanceReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckAndNotifyAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in AttendanceReminderBackgroundService.");
            }
        }
    }

    private async Task CheckAndNotifyAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DemizonContext>();
        var fcm = scope.ServiceProvider.GetRequiredService<FcmService>();

        var now = DateTime.UtcNow;

        var upcomingEvents = await db.Events
            .Where(e => !e.IsCancelled
                && e.NotifyBeforeDays != null
                && e.DateFrom > now
                // Notifikace se odesílá jen v 6h okně kdy akce poprvé vstoupí do prahu.
                // Např. NotifyBeforeDays=3: okno = (now+3d-6h, now+3d].
                // Každá akce projde tímto oknem právě jednou → max 1 notifikace na tick.
                && e.DateFrom > now.AddDays(e.NotifyBeforeDays!.Value).AddHours(-6)
                && e.DateFrom <= now.AddDays(e.NotifyBeforeDays!.Value))
            .ToListAsync(ct);

        foreach (var ev in upcomingEvents)
        {
            var attendedMemberIds = await db.Attendances
                .Where(a => a.EventId == ev.Id)
                .Select(a => a.MemberId)
                .ToListAsync(ct);

            var membersWithoutAttendance = await db.Members
                .Where(m => !attendedMemberIds.Contains(m.Id))
                .Select(m => new { m.Id })
                .ToListAsync(ct);

            var memberIds = membersWithoutAttendance.Select(m => m.Id).ToList();

            var deviceTokens = await db.DeviceTokens
                .Where(d => memberIds.Contains(d.MemberId))
                .ToListAsync(ct);

            foreach (var dt in deviceTokens)
            {
                await fcm.SendAsync(
                    dt.Token,
                    "Nevyplněná docházka",
                    $"Nezapomeň vyplnit docházku na akci: {ev.Name}",
                    new Dictionary<string, string> { ["eventId"] = ev.Id.ToString() });
            }
        }
    }
}
