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
using Microsoft.Extensions.Logging;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/attendances")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AttendancesController(
    IAttendanceService attendanceService,
    IEventService eventService,
    IMemberService memberService,
    IGoogleCalendarService googleCalendarService,
    IAttendanceReportService reportService,
    ILogger<AttendancesController> logger) : ControllerBase
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

        attendance.Status = ParseStatus(request.Status);
        attendance.Comment = request.Comment;
        attendance.ActivityRole = activityRole;

        // Google Calendar sync — pouze PŘED uložením do DB
        var member = await memberService.GetOneAsync(memberId);
        var gcalWarning = (string?)null;
        if (!string.IsNullOrEmpty(member.GoogleRefreshToken) && !string.IsNullOrEmpty(member.GoogleCalendarId))
        {
            try
            {
                if (attendance.Status == AttendanceStatus.Yes)
                {
                    if (string.IsNullOrEmpty(attendance.GoogleEventId))
                    {
                        logger.LogInformation(
                            "Vytvářím GCal událost pro člena {MemberId}, akce {EventId} ({EventName}), DateFrom={DateFrom}, DateTo={DateTo}.",
                            memberId, ev.Id, ev.Name, ev.DateFrom, ev.DateTo);
                        var googleEventId = await googleCalendarService.CreateEventAsync(
                            member.GoogleRefreshToken, member.GoogleCalendarId, ev.DateFrom, ev.DateTo, ev.Name);
                        if (googleEventId is not null)
                        {
                            attendance.GoogleEventId = googleEventId;
                        }
                        else
                        {
                            logger.LogWarning("GCal CreateEvent vrátil null pro člena {MemberId}, akce {EventId}.", memberId, ev.Id);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(attendance.GoogleEventId))
                {
                    await googleCalendarService.DeleteEventAsync(
                        member.GoogleRefreshToken, member.GoogleCalendarId, attendance.GoogleEventId);
                    attendance.GoogleEventId = null;
                }
            }
            catch (Common.Exceptions.GoogleTokenRevokedException)
            {
                logger.LogWarning("Google token pro člena {MemberId} byl odvolán — mažu uložené credentials.", memberId);
                await memberService.DisconnectGoogleCalendarAsync(memberId);
                gcalWarning = "Propojení s Google Calendar vypršelo. Prosím znovu propojte v nastavení profilu.";
            }
        }
        else
        {
            logger.LogDebug(
                "GCal sync přeskočen pro člena {MemberId}: RefreshToken={HasToken}, CalendarId={HasCalId}.",
                memberId, !string.IsNullOrEmpty(member.GoogleRefreshToken), !string.IsNullOrEmpty(member.GoogleCalendarId));
        }

        // TEPRVE POTOM ulož do DB (s GoogleEventId buď vyplněným nebo null)
        await attendanceService.CreateOrUpdateAsync(attendance);

        if (gcalWarning is not null)
            Response.Headers.Append("X-GCal-Warning", gcalWarning);
        return Ok(attendance.ToDto());
    }

    [HttpGet("stats")]
    public async Task<ActionResult<List<MemberAttendanceStatDto>>> GetStats(
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var stats = await reportService.GetMemberStatsAsync(from, to);
        return Ok(stats.Select(s => new MemberAttendanceStatDto(
            s.MemberId, s.FullName,
            s.TotalRehearsals, s.AttendedRehearsals, s.RehearsalRate,
            s.TotalActions, s.AttendedActions, s.ActionRate)).ToList());
    }

    [HttpPut("rehearsal")]
    public async Task<ActionResult<AttendanceDto>> UpsertRehearsal(
        [FromQuery] DateTime date, [FromBody] UpsertAttendanceRequest request)
    {
        var memberId = User.GetMemberId();
        var day = date.Date;

        var existing = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == null && a.Date == day);

        var attendance = existing ?? new Dal.Entities.Attendance
        {
            MemberId = memberId,
            EventId = null,
            Date = day,
        };

        attendance.Status = ParseStatus(request.Status);
        attendance.Comment = request.Comment;

        // Google Calendar sync pro zkoušky
        var member = await memberService.GetOneAsync(memberId);
        var gcalWarning = (string?)null;
        if (!string.IsNullOrEmpty(member.GoogleRefreshToken) && !string.IsNullOrEmpty(member.GoogleCalendarId))
        {
            try
            {
                if (attendance.Status == AttendanceStatus.Yes)
                {
                    if (string.IsNullOrEmpty(attendance.GoogleEventId))
                    {
                        var title = $"Zkouška Demizon – {day:d. M. yyyy}";
                        var googleEventId = await googleCalendarService.CreateEventAsync(
                            member.GoogleRefreshToken, member.GoogleCalendarId, day, null, title);
                        if (googleEventId is not null)
                        {
                            attendance.GoogleEventId = googleEventId;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(attendance.GoogleEventId))
                {
                    await googleCalendarService.DeleteEventAsync(
                        member.GoogleRefreshToken, member.GoogleCalendarId, attendance.GoogleEventId);
                    attendance.GoogleEventId = null;
                }
            }
            catch (Common.Exceptions.GoogleTokenRevokedException)
            {
                logger.LogWarning("Google token pro člena {MemberId} byl odvolán — mažu uložené credentials.", memberId);
                await memberService.DisconnectGoogleCalendarAsync(memberId);
                gcalWarning = "Propojení s Google Calendar vypršelo. Prosím znovu propojte v nastavení profilu.";
            }
        }

        await attendanceService.CreateOrUpdateAsync(attendance);

        if (gcalWarning is not null)
            Response.Headers.Append("X-GCal-Warning", gcalWarning);
        var result = attendance.ToDto();
        return Ok(result);
    }

    /// <summary>
    /// Admin: gets a specific member's attendance for an event.
    /// </summary>
    [HttpGet("{eventId:int}/member/{memberId:int}")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AttendanceDto>> GetMemberAttendance(int eventId, int memberId)
    {
        var att = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == eventId);

        if (att is null)
        {
            try { await eventService.GetOneAsync(eventId); }
            catch (EntityNotFoundException) { return NotFound(); }
            return Ok(new AttendanceDto(0, "no", null, null, DateTime.MinValue));
        }

        return Ok(att.ToDto());
    }

    /// <summary>
    /// Admin: upserts a specific member's attendance for an event.
    /// </summary>
    [HttpPut("{eventId:int}/member/{memberId:int}")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AttendanceDto>> UpsertMemberAttendance(int eventId, int memberId, [FromBody] UpsertAttendanceRequest request)
    {
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

        attendance.Status = ParseStatus(request.Status);
        attendance.Comment = request.Comment;
        attendance.ActivityRole = activityRole;

        // Google Calendar sync for the target member
        try
        {
            var member = await memberService.GetOneAsync(memberId);
            if (!string.IsNullOrEmpty(member.GoogleRefreshToken) && !string.IsNullOrEmpty(member.GoogleCalendarId))
            {
                if (attendance.Status == AttendanceStatus.Yes)
                {
                    if (string.IsNullOrEmpty(attendance.GoogleEventId))
                    {
                        var googleEventId = await googleCalendarService.CreateEventAsync(
                            member.GoogleRefreshToken, member.GoogleCalendarId, ev.DateFrom, ev.DateTo, ev.Name);
                        if (googleEventId is not null)
                        {
                            attendance.GoogleEventId = googleEventId;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(attendance.GoogleEventId))
                {
                    await googleCalendarService.DeleteEventAsync(
                        member.GoogleRefreshToken, member.GoogleCalendarId, attendance.GoogleEventId);
                    attendance.GoogleEventId = null;
                }
            }
        }
        catch (Common.Exceptions.GoogleTokenRevokedException)
        {
            logger.LogWarning("Google token pro člena {MemberId} byl odvolán — mažu uložené credentials.", memberId);
            await memberService.DisconnectGoogleCalendarAsync(memberId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google Calendar sync selhal pro člena {MemberId}, akce {EventId}.", memberId, eventId);
        }

        await attendanceService.CreateOrUpdateAsync(attendance);
        return Ok(attendance.ToDto());
    }

    /// <summary>
    /// Admin: gets a specific member's rehearsal attendance for a date.
    /// </summary>
    [HttpGet("rehearsal/member/{memberId:int}")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AttendanceDto>> GetMemberRehearsal(int memberId, [FromQuery] DateTime date)
    {
        var day = date.Date;
        var att = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == null && a.Date == day);

        if (att is null)
            return Ok(new AttendanceDto(0, "no", null, null, DateTime.MinValue));

        return Ok(att.ToDto());
    }

    /// <summary>
    /// Admin: upserts a specific member's rehearsal attendance for a date.
    /// </summary>
    [HttpPut("rehearsal/member/{memberId:int}")]
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AttendanceDto>> UpsertMemberRehearsal(int memberId, [FromQuery] DateTime date, [FromBody] UpsertAttendanceRequest request)
    {
        var day = date.Date;

        var existing = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == null && a.Date == day);

        var attendance = existing ?? new Dal.Entities.Attendance
        {
            MemberId = memberId,
            EventId = null,
            Date = day,
        };

        attendance.Status = ParseStatus(request.Status);
        attendance.Comment = request.Comment;

        await attendanceService.CreateOrUpdateAsync(attendance);
        return Ok(attendance.ToDto());
    }

    [HttpGet("rehearsal")]
    public async Task<ActionResult<AttendanceDto>> GetRehearsal([FromQuery] DateTime date)
    {
        var memberId = User.GetMemberId();
        var day = date.Date;

        var att = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == null && a.Date == day);

        if (att is null) return NotFound();
        return Ok(att.ToDto());
    }

    [HttpDelete("{eventId:int}")]
    public async Task<IActionResult> DeleteEventAttendance(int eventId)
    {
        var memberId = User.GetMemberId();

        var att = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == eventId);

        if (att is null) return NoContent();

        // Clean up Google Calendar event before deleting
        try
        {
            var member = await memberService.GetOneAsync(memberId);
            if (!string.IsNullOrEmpty(member.GoogleRefreshToken) &&
                !string.IsNullOrEmpty(member.GoogleCalendarId) &&
                !string.IsNullOrEmpty(att.GoogleEventId))
            {
                await googleCalendarService.DeleteEventAsync(
                    member.GoogleRefreshToken, member.GoogleCalendarId, att.GoogleEventId);
            }
        }
        catch
        {
            // Google Calendar sync failure must not block deletion
        }

        await attendanceService.DeleteAsync(att.Id);
        return NoContent();
    }

    [HttpDelete("rehearsal")]
    public async Task<IActionResult> DeleteRehearsalAttendance([FromQuery] DateTime date)
    {
        var memberId = User.GetMemberId();
        var day = date.Date;

        var att = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == null && a.Date == day);

        if (att is null) return NoContent();

        // Clean up Google Calendar event before deleting
        try
        {
            var member = await memberService.GetOneAsync(memberId);
            if (!string.IsNullOrEmpty(member.GoogleRefreshToken) &&
                !string.IsNullOrEmpty(member.GoogleCalendarId) &&
                !string.IsNullOrEmpty(att.GoogleEventId))
            {
                await googleCalendarService.DeleteEventAsync(
                    member.GoogleRefreshToken, member.GoogleCalendarId, att.GoogleEventId);
            }
        }
        catch
        {
            // Google Calendar sync failure must not block deletion
        }

        await attendanceService.DeleteAsync(att.Id);
        return NoContent();
    }

    [HttpGet("table")]
    public async Task<ActionResult<MonthlyAttendanceTableDto>> GetMonthlyTable(
        [FromQuery] int year, [FromQuery] int month)
    {
        if (month < 1 || month > 12 || year < 2000 || year > 2100)
            return BadRequest("Neplatný rok nebo měsíc.");

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var events = await eventService.GetAll()
            .Where(e => e.DateFrom >= from && e.DateFrom < to)
            .OrderBy(e => e.DateFrom)
            .ToListAsync();

        var eventDates = events.Select(e => e.DateFrom.Date).ToHashSet();

        var fridayColumns = Enumerable
            .Range(0, (to - from).Days)
            .Select(i => from.AddDays(i))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday && !eventDates.Contains(d.Date))
            .Select(d => new MonthlyColumnDto(null, $"Zkouška {d:d.M.}", d, false, false));

        var eventColumns = events
            .Select(e => new MonthlyColumnDto(e.Id, e.Name, e.DateFrom,
                e.Recurrence != RecurrenceType.Weekly, e.IsCancelled));

        var columns = fridayColumns.Concat(eventColumns).OrderBy(c => c.Date).ToList();

        var members = await memberService.GetAll()
            .Where(m => m.IsAttendanceVisible)
            .OrderBy(m => m.Surname).ThenBy(m => m.Name)
            .ToListAsync();

        var memberIds = members.Select(m => m.Id).ToList();
        var attendances = await attendanceService.GetMembersAttendancesAsync(memberIds, from, to.AddTicks(-1));

        var memberRows = members.Select(m =>
        {
            var memberAtts = attendances.Where(a => a.MemberId == m.Id).ToList();
            var cells = columns.Select(col =>
            {
                Dal.Entities.Attendance? att = col.EventId.HasValue
                    ? memberAtts.FirstOrDefault(a => a.EventId == col.EventId)
                    : memberAtts.FirstOrDefault(a => a.EventId == null && a.Date.Date == col.Date.Date);
                return new MemberCellDto(col.Date, col.EventId, att?.Status.ToString().ToLowerInvariant(), att?.Comment);
            }).ToList();
            return new MemberMonthlyRowDto(m.Id, $"{m.Name} {m.Surname}", cells);
        }).ToList();

        return Ok(new MonthlyAttendanceTableDto(columns, memberRows));
    }

    private static AttendanceStatus ParseStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "yes" => AttendanceStatus.Yes,
        "maybe" => AttendanceStatus.Maybe,
        _ => AttendanceStatus.No
    };
}