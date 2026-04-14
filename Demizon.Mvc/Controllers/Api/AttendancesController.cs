using Demizon.Common.Exceptions;
using Demizon.Contracts.Attendances;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Dal.Entities;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/attendances")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AttendancesController(
    IAttendanceService attendanceService,
    IEventService eventService,
    IMemberService memberService,
    IGoogleCalendarService googleCalendarService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<List<AttendanceDto>>> GetMyAttendances()
    {
        var memberId = User.GetMemberId();

        var attendances = await attendanceService.GetAll()
            .Where(a => a.MemberId == memberId)
            .Include(a => a.Event)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        return Ok(attendances.Select(a => a.ToDto()).ToList());
    }

    [HttpPut("{eventId:int}")]
    public async Task<ActionResult<AttendanceDto>> Upsert(int eventId, [FromBody] UpsertAttendanceRequest request)
    {
        var memberId = User.GetMemberId();

        Dal.Entities.Event ev;
        try { ev = await eventService.GetOneAsync(eventId); }
        catch (EntityNotFoundException) { return NotFound(); }

        AttendanceActivityRole? activityRole = request.ActivityRole?.ToLowerInvariant() switch
        {
            "dancer" => AttendanceActivityRole.Dancer,
            "musician" => AttendanceActivityRole.Musician,
            _ => null
        };

        var existing = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == eventId);

        var attendance = existing ?? new Dal.Entities.Attendance
        {
            MemberId = memberId,
            EventId = eventId,
            Date = ev.DateFrom,
        };

        attendance.Attends = request.Attends;
        attendance.Comment = request.Comment;
        attendance.ActivityRole = activityRole;

        await attendanceService.CreateOrUpdateAsync(attendance);

        // Google Calendar sync
        var member = await memberService.GetOneAsync(memberId);
        if (!string.IsNullOrEmpty(member.GoogleRefreshToken) && !string.IsNullOrEmpty(member.GoogleCalendarId))
        {
            if (request.Attends)
            {
                if (string.IsNullOrEmpty(attendance.GoogleEventId))
                {
                    var googleEventId = await googleCalendarService.CreateEventAsync(
                        member.GoogleRefreshToken, member.GoogleCalendarId, ev.DateFrom, ev.Name);
                    if (googleEventId is not null)
                    {
                        attendance.GoogleEventId = googleEventId;
                        await attendanceService.CreateOrUpdateAsync(attendance);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(attendance.GoogleEventId))
            {
                await googleCalendarService.DeleteEventAsync(
                    member.GoogleRefreshToken, member.GoogleCalendarId, attendance.GoogleEventId);
                attendance.GoogleEventId = null;
                await attendanceService.CreateOrUpdateAsync(attendance);
            }
        }

        return Ok(attendance.ToDto());
    }
}
