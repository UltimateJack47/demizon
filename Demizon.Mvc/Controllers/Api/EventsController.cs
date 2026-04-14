using Demizon.Contracts.Events;
using Demizon.Core.Services.Event;
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
public class EventsController(IEventService eventService) : ControllerBase
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
}
