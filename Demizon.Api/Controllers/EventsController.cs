using Demizon.Api.Extensions;
using Demizon.Api.Mapping;
using Demizon.Contracts.Events;
using Demizon.Core.Services.Event;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
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
}
