using Demizon.Contracts.Events;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Event;
using Demizon.Dal.Entities;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/events")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class EventsController(IEventService eventService, IAttendanceService attendanceService) : ControllerBase
{
    [HttpGet("upcoming")]
    public async Task<ActionResult<List<EventDto>>> GetUpcoming()
    {
        var memberId = User.GetMemberId();
        var today = DateTime.UtcNow.Date;

        var events = await eventService.GetAll()
            .Where(e => e.DateFrom >= today)
            .Include(e => e.Attendances)
            .OrderBy(e => e.DateFrom)
            .ToListAsync();

        var result = events.Select(e =>
        {
            var myAttendance = e.Attendances.FirstOrDefault(a => a.MemberId == memberId);
            return e.ToDto(myAttendance?.ToDto());
        }).ToList();

        return Ok(result);
    }

    [HttpGet("month")]
    public async Task<ActionResult<List<EventDto>>> GetByMonth([FromQuery] int year, [FromQuery] int month)
    {
        var memberId = User.GetMemberId();
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        // Load events for the month
        var events = await eventService.GetAll()
            .Where(e => e.DateFrom >= from && e.DateFrom < to)
            .Include(e => e.Attendances)
            .OrderBy(e => e.DateFrom)
            .ToListAsync();

        var eventDates = events.Select(e => e.DateFrom.Date).ToHashSet();

        // Fridays in the month that have no event = rehearsals (zkoušky)
        var rehearsalFridays = Enumerable
            .Range(0, (to - from).Days)
            .Select(d => from.AddDays(d))
            .Where(d => d.DayOfWeek == DayOfWeek.Friday && !eventDates.Contains(d.Date))
            .ToList();

        // Load rehearsal attendances (EventId == null, Date matches a Friday)
        var rehearsalAttendances = rehearsalFridays.Count > 0
            ? await attendanceService.GetAll()
                .Where(a => a.MemberId == memberId && a.EventId == null && rehearsalFridays.Contains(a.Date))
                .ToListAsync()
            : [];

        var result = new List<EventDto>();

        // Add events
        result.AddRange(events.Select(e =>
        {
            var myAttendance = e.Attendances.FirstOrDefault(a => a.MemberId == memberId);
            return e.ToDto(myAttendance?.ToDto());
        }));

        // Add rehearsal rows
        result.AddRange(rehearsalFridays.Select(friday =>
        {
            var myAttendance = rehearsalAttendances.FirstOrDefault(a => a.Date == friday);
            return new EventDto(0, "Zkouška", friday, friday.AddHours(2),
                null, false, "none", myAttendance?.ToDto(), IsRehearsal: true);
        }));

        return Ok(result.OrderBy(e => e.DateFrom).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventDto>> GetOne(int id)
    {
        var memberId = User.GetMemberId();

        try
        {
            var e = await eventService.GetOneAsync(id);
            var myAttendance = e.Attendances.FirstOrDefault(a => a.MemberId == memberId);
            return Ok(e.ToDto(myAttendance?.ToDto()));
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<ActionResult<EventDto>> Create([FromBody] CreateEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Název akce je povinný." });

        if (request.DateFrom >= request.DateTo)
            return BadRequest(new { error = "Datum začátku musí být před datem konce." });

        var recurrence = request.Recurrence?.ToLowerInvariant() switch
        {
            "weekly" => Dal.Entities.RecurrenceType.Weekly,
            "monthly" => Dal.Entities.RecurrenceType.Monthly,
            _ => Dal.Entities.RecurrenceType.None
        };

        var ev = new Dal.Entities.Event
        {
            Name = request.Name,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Place = request.Place,
            NotifyBeforeDays = request.NotifyBeforeDays,
            Recurrence = recurrence,
        };

        var success = await eventService.CreateAsync(ev);
        if (!success)
            return StatusCode(500, new { error = "Failed to create event." });

        return CreatedAtAction(nameof(GetOne), new { id = ev.Id }, ev.ToDto());
    }

    [HttpGet("{id:int}/attendees")]
    public async Task<ActionResult<EventAttendeesDto>> GetAttendees(int id)
    {
        var attendees = await attendanceService.GetAll()
            .Where(a => a.EventId == id && a.Status == AttendanceStatus.Yes)
            .Include(a => a.Member)
            .ToListAsync();

        var dtos = attendees
            .Select(a => new EventAttendeeDto(
                a.MemberId,
                $"{a.Member.Name} {a.Member.Surname}",
                a.ActivityRole?.ToString().ToLowerInvariant()))
            .OrderBy(a => a.FullName)
            .ToList();

        var dancerCount = dtos.Count(a => a.ActivityRole == "dancer");
        var musicianCount = dtos.Count(a => a.ActivityRole == "musician");

        return Ok(new EventAttendeesDto(dtos, dancerCount, musicianCount, dtos.Count));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Název akce je povinný." });

        if (request.DateFrom >= request.DateTo)
            return BadRequest(new { error = "Datum začátku musí být před datem konce." });

        try
        {
            var ev = await eventService.GetOneAsync(id);

            var recurrence = request.Recurrence?.ToLowerInvariant() switch
            {
                "weekly" => RecurrenceType.Weekly,
                "monthly" => RecurrenceType.Monthly,
                _ => RecurrenceType.None
            };

            ev.Name = request.Name;
            ev.DateFrom = request.DateFrom;
            ev.DateTo = request.DateTo;
            ev.Place = request.Place;
            ev.NotifyBeforeDays = request.NotifyBeforeDays;
            ev.Recurrence = recurrence;
            ev.IsPublic = request.IsPublic;
            ev.IsCancelled = request.IsCancelled;

            await eventService.UpdateAsync(id, ev);
            return Ok(ev.ToDto());
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await eventService.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleCancelled(int id)
    {
        try
        {
            var ev = await eventService.GetOneAsync(id);
            await eventService.SetCancelledAsync(id, !ev.IsCancelled);
            return Ok();
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("{id:int}/public")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TogglePublic(int id)
    {
        try
        {
            var ev = await eventService.GetOneAsync(id);
            ev.IsPublic = !ev.IsPublic;
            await eventService.UpdateAsync(id, ev);
            return Ok();
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }
}
