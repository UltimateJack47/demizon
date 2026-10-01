using Demizon.Contracts.Rehearsals;
using Demizon.Core.Services.Rehearsal;
using Demizon.Dal.Entities;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Services.Rehearsal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demizon.Mvc.Controllers.Api;

/// <summary>
/// Výjimky z pravidelných zkoušek. Samotné zkoušky žádný endpoint nemají —
/// chodí jako sloupce tabulky (<c>/api/attendances/table</c>) a řádky měsíce
/// (<c>/api/events/month</c>).
/// </summary>
[ApiController]
[Route("api/rehearsals")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RehearsalsController(
    IRehearsalService rehearsalService,
    RehearsalWorkflow workflow) : ControllerBase
{
    /// <summary>
    /// Výjimky, které zasahují do <paramref name="from"/>–<paramref name="to"/> (obojí včetně).
    /// Bez parametrů od dneška na rok dopředu.
    /// </summary>
    [HttpGet("cancellations")]
    public async Task<ActionResult<List<RehearsalCancellationDto>>> GetCancellations(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.Today);
        var end = to ?? start.AddYears(1);
        if (end < start)
            return BadRequest(new { error = "Konec období je před začátkem." });

        var cancellations = await rehearsalService.GetCancellationsAsync(start, end.AddDays(1), ct);
        return Ok(cancellations.Select(ToDto).ToList());
    }

    [HttpPost("cancellations")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<ActionResult<RehearsalCancellationDto>> Cancel(
        [FromBody] CancelRehearsalsRequest request, CancellationToken ct)
    {
        // Přes workflow, ne rovnou službu: zrušení smaže členům události
        // v Google Calendaru a s Notify pošle push.
        var created = await workflow.CancelAsync(
            request.DateFrom, request.DateTo, request.Reason, User.GetMemberId(), request.Notify, ct);
        if (!created.IsSuccess)
            return created.ToErrorResponse();

        var cancellation = await rehearsalService.GetCancellationAsync(created.Value!.CancellationId, ct);
        return CreatedAtAction(nameof(GetCancellations), null, ToDto(cancellation!));
    }

    [HttpDelete("cancellations/{id:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        var restored = await workflow.RestoreAsync(id, ct);
        return restored.IsSuccess ? NoContent() : restored.ToErrorResponse();
    }

    private static RehearsalCancellationDto ToDto(RehearsalCancellation c) =>
        new(c.Id, c.DateFrom, c.DateTo, c.Reason, c.CreatedAt,
            c.CreatedBy is null ? null : $"{c.CreatedBy.Name} {c.CreatedBy.Surname}");
}
