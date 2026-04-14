using Demizon.Api.Mapping;
using Demizon.Contracts.Dances;
using Demizon.Core.Services.Dance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Api.Controllers;

[ApiController]
[Route("api/dances")]
[Authorize]
public class DancesController(IDanceService danceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DanceDto>>> GetAll()
    {
        var dances = await danceService.GetAll()
            .Where(d => d.IsVisible)
            .Include(d => d.Videos)
            .OrderBy(d => d.Name)
            .ToListAsync();

        return Ok(dances.Select(d => d.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DanceDto>> GetOne(int id)
    {
        try
        {
            var dance = await danceService.GetOneAsync(id);
            if (!dance.IsVisible) return NotFound();
            return Ok(dance.ToDto());
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }
}
