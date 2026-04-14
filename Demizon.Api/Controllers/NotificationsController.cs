using Demizon.Api.Extensions;
using Demizon.Contracts.Notifications;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(DemizonContext db) : ControllerBase
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
}
