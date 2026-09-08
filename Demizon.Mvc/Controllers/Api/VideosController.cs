using Demizon.Contracts.Dances;
using Demizon.Core.Services.VideoLink;
using Demizon.Mvc.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Demizon.Mvc.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/videos")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class VideosController(IVideoLinkService videoLinkService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VideoLinkDto>>> GetAll()
    {
        var videos = await videoLinkService.GetAll()
            .OrderByDescending(v => v.Year)
            .ToListAsync();

        return Ok(videos.Select(v => v.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VideoLinkDto>> GetOne(int id)
    {
        try
        {
            var video = await videoLinkService.GetOneAsync(id);
            return Ok(video.ToDto());
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<ActionResult<VideoLinkDto>> Create([FromBody] CreateVideoLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Název videa je povinný." });

        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "URL videa je povinná." });

        var entity = new Dal.Entities.VideoLink
        {
            Name = request.Name,
            Url = request.Url,
            Year = request.Year,
            IsVisible = request.IsVisible,
            IsInternal = request.IsInternal,
            DanceId = request.DanceId,
        };

        var created = await videoLinkService.CreateAsync(entity);
        if (!created.IsSuccess)
            return created.ToErrorResponse();

        return CreatedAtAction(nameof(GetOne), new { id = entity.Id }, entity.ToDto());
    }

    [HttpPut("{id:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateVideoLinkRequest request)
    {
        try
        {
            var entity = await videoLinkService.GetOneAsync(id);

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Název videa je povinný." });

            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(new { error = "URL videa je povinná." });

            entity.Name = request.Name;
            entity.Url = request.Url;
            entity.Year = request.Year;
            entity.IsVisible = request.IsVisible;
            entity.IsInternal = request.IsInternal;
            entity.DanceId = request.DanceId;

            await videoLinkService.UpdateAsync(id, entity);
            return Ok(entity.ToDto());
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await videoLinkService.DeleteAsync(id);
        // Dřív se každé selhání hlásilo jako 404, včetně chyby zápisu.
        return deleted.IsSuccess ? NoContent() : deleted.ToErrorResponse();
    }
}
