using Demizon.Api.Extensions;
using Demizon.Api.Mapping;
using Demizon.Common.Exceptions;
using Demizon.Contracts.Attendances;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Dal.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Api.Controllers;

[ApiController]
[Route("api/attendances")]
[Authorize]
public class AttendancesController(
    IAttendanceService attendanceService,
    IEventService eventService,
    IMemberService memberService,
    IGoogleCalendarService googleCalendarService,
    IAttendanceReportService reportService) : ControllerBase
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

        await attendanceService.CreateOrUpdateAsync(attendance);

        // Google Calendar sync — only create event when Yes; delete when switching to No or Maybe
        var member = await memberService.GetOneAsync(memberId);
        if (!string.IsNullOrEmpty(member.GoogleRefreshToken) && !string.IsNullOrEmpty(member.GoogleCalendarId))
        {
            if (attendance.Status == AttendanceStatus.Yes)
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

    /// <summary>
    /// Admin: gets a specific member's attendance for an event.
    /// </summary>
    [HttpGet("{eventId:int}/member/{memberId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AttendanceDto>> GetMemberAttendance(int eventId, int memberId)
    {
        var att = await attendanceService.GetAll()
            .FirstOrDefaultAsync(a => a.MemberId == memberId && a.EventId == eventId);

        if (att is null)
        {
            // Verify the event exists; return a blank DTO when no attendance record exists yet
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
    [Authorize(Roles = "Admin")]
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

        await attendanceService.CreateOrUpdateAsync(attendance);

        // Google Calendar sync for the target member — only create when Yes, delete on No/Maybe
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
        }
        catch
        {
            // Google Calendar sync failure must not block saving attendance
        }

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

    [HttpGet("table")]
    public async Task<ActionResult<MonthlyAttendanceTableDto>> GetMonthlyTable(
        [FromQuery] int year, [FromQuery] int month)
    {
        if (month < 1 || month > 12 || year < 2000 || year > 2100)
            return BadRequest("Neplatný rok nebo měsíc.");

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        // Load all events for the month
        var events = await eventService.GetAll()
            .Where(e => e.DateFrom >= from && e.DateFrom < to)
            .OrderBy(e => e.DateFrom)
            .ToListAsync();

        // Build columns: events override the Friday slot; standalone Fridays become rehearsal columns
        var eventDates = events.Select(e => e.DateFrom.Date).ToHashSet();
        var fridayColumns = Enumerable
            .Range(0, (int)(to - from).TotalDays)
            .Select(i => from.AddDays(i))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday && !eventDates.Contains(d.Date))
            .Select(d => new MonthlyColumnDto(null, $"Zkouška {d:d.M.}", d, false, false));

        var eventColumns = events
            .Select(e => new MonthlyColumnDto(e.Id, e.Name, e.DateFrom, e.Recurrence != Dal.Entities.RecurrenceType.Weekly, e.IsCancelled));

        var columns = fridayColumns
            .Concat(eventColumns)
            .OrderBy(c => c.Date)
            .ToList();

        // Load all visible members
        var members = await memberService.GetAll()
            .Where(m => m.IsAttendanceVisible)
            .OrderBy(m => m.Surname).ThenBy(m => m.Name)
            .ToListAsync();

        var memberIds = members.Select(m => m.Id).ToList();

        // Use to.AddTicks(-1) to cover events at any time on the last day of the month
        var attendances = await attendanceService.GetMembersAttendancesAsync(memberIds, from, to.AddTicks(-1));

        var memberRows = members.Select(m =>
        {
            var memberAtts = attendances.Where(a => a.MemberId == m.Id).ToList();
            var cells = columns.Select(col =>
            {
                Dal.Entities.Attendance? att = col.EventId.HasValue
                    ? memberAtts.FirstOrDefault(a => a.EventId == col.EventId)
                    : memberAtts.FirstOrDefault(a => a.EventId == null && a.Date.Date == col.Date.Date);
                return new MemberCellDto(col.Date, col.EventId, att?.Status.ToString().ToLowerInvariant());
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
