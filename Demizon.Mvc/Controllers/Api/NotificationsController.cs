using Demizon.Contracts.Notifications;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationsController(DemizonContext db, FcmService fcm) : ControllerBase
{
    [HttpPost("device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var memberId = User.GetMemberId();

        var platform = request.Platform.ToLowerInvariant() switch
        {
            "android" => DevicePlatform.Android,
            "ios" => DevicePlatform.Ios,
            _ => (DevicePlatform?)null
        };

        if (platform is null)
            return BadRequest(new { error = "Invalid platform. Use 'android' or 'ios'." });

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == request.Token);
        if (existing is not null)
        {
            existing.LastSeenAt = DateTime.UtcNow;
            existing.MemberId = memberId;
            existing.Platform = platform.Value;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                MemberId = memberId,
                Token = request.Token,
                Platform = platform.Value,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("device")]
    public async Task<IActionResult> UnregisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var memberId = User.GetMemberId();
        var token = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == request.Token);
        if (token is null) return NotFound();

        if (token.MemberId != memberId) return Forbid();

        db.DeviceTokens.Remove(token);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<IActionResult> SendTestNotification()
    {
        var memberId = User.GetMemberId();
        var deviceTokens = await db.DeviceTokens.Where(d => d.MemberId == memberId).ToListAsync();

        if (deviceTokens.Count == 0)
            return BadRequest(new { error = "Žádné registrované zařízení nenalezeno." });

        var body = $"Test odeslaný v {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        var anySent = false;
        foreach (var dt in deviceTokens)
        {
            var result = await fcm.SendAsync(dt.Token, "Test notifikace 🔔", body);
            if (result == FcmSendResult.Success)
            {
                anySent = true;
            }
            else if (result == FcmSendResult.InvalidToken)
            {
                db.DeviceTokens.Remove(dt);
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        if (!anySent)
            return StatusCode(503, new { error = "Firebase není nakonfigurován nebo odeslání selhalo." });

        return NoContent();
    }
}
