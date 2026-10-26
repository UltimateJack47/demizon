using Demizon.Common;
using Demizon.Contracts.Costumes;
using Demizon.Core.Services.Costume;
using Demizon.Dal.Entities;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Mapping;
using Demizon.Mvc.Services.Authentication;
using Demizon.Mvc.Services.Costume;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demizon.Mvc.Controllers.Api;

/// <summary>
/// Krojárka. Každý člen vidí, co drží, předávky, kterých se účastní, a kartu kusu z QR.
/// Celou krojárku vidí správce, admin a vůdce souboru; měnit katalog smí jen správce.
/// Pravidla předávek (kdo co smí založit a potvrdit) hlídá <see cref="ICostumeTransferService"/>.
/// </summary>
[ApiController]
[Route("api/costumes")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CostumesController(
    ICostumeCatalogService catalog,
    ICostumeTransferService transfers,
    CostumeQrService qrService,
    CostumeQrCodes qrCodes) : ControllerBase
{
    private const string Scheme = JwtBearerDefaults.AuthenticationScheme;

    private bool IsKeeper => User.IsInRole(RoleNames.CostumeKeeper);

    private bool IsViewer => IsKeeper || User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Leader);

    private CostumeActor Actor => new(User.GetMemberId(), IsKeeper);

    // ------------------------------------------------------------- člen

    [HttpGet("me")]
    public async Task<ActionResult<MyCostumesDto>> GetMine(CancellationToken ct)
    {
        var memberId = User.GetMemberId();
        var holdings = await catalog.GetHoldingsAsync(memberId, ct);
        var mine = await transfers.GetForMemberAsync(memberId, ct);
        var incoming = mine.Where(t => t.Status == CostumeTransferStatus.Pending && t.ToMemberId == memberId).ToList();
        var outgoing = mine.Where(t => t.CreatedById == memberId
                                       && t.Status is CostumeTransferStatus.Draft or CostumeTransferStatus.Pending)
            .ToList();

        return Ok(new MyCostumesDto(
            holdings.Select(h => new CostumeHoldingDto(h.Item.ToDto(IsViewer), h.Quantity)).ToList(),
            await ToDtosAsync(incoming, ct),
            await ToDtosAsync(outgoing, ct)));
    }

    /// <summary>Historie předávek člena, nejnovější první.</summary>
    [HttpGet("me/transfers")]
    public async Task<ActionResult<List<CostumeTransferDto>>> GetMyTransfers(CancellationToken ct) =>
        Ok(await ToDtosAsync(await transfers.GetForMemberAsync(User.GetMemberId(), ct), ct));

    /// <summary>QR z profilu jako PNG. Podle něj správce nebo kolega pozná, komu předává.</summary>
    [HttpGet("me/qr")]
    public async Task<IActionResult> GetMyQr(CancellationToken ct)
    {
        var token = await qrService.GetOrCreateTokenAsync(User.GetMemberId(), ct);
        return token.IsSuccess
            ? File(CostumeQrCodes.Png(qrCodes.MemberUrl(token.Value!)), "image/png")
            : token.ToErrorResponse();
    }

    /// <summary>Nový QR; starý (třeba vyfocený a poslaný dál) přestane platit.</summary>
    [HttpPost("me/qr/regenerate")]
    public async Task<IActionResult> RegenerateMyQr(CancellationToken ct)
    {
        var token = await qrService.RegenerateTokenAsync(User.GetMemberId(), ct);
        return token.IsSuccess ? NoContent() : token.ToErrorResponse();
    }

    [HttpGet("members/by-qr/{token}")]
    public async Task<ActionResult<CostumeMemberDto>> GetMemberByQr(string token, CancellationToken ct)
    {
        var member = await qrService.FindMemberAsync(token, ct);
        return member is null
            ? NotFound(new { error = "QR kód nepatří žádnému aktivnímu členovi." })
            : Ok(new CostumeMemberDto(member.Id, $"{member.Name} {member.Surname}"));
    }

    [HttpGet("items/by-code/{code}")]
    public async Task<ActionResult<CostumeItemCardDto>> GetItemCard(string code, CancellationToken ct)
    {
        var item = await catalog.GetItemByCodeAsync(code, ct);
        if (item is null)
            return NotFound(new { error = "Kus s tímto kódem neexistuje." });

        var holdings = (await catalog.GetHoldingsAsync(ct: ct)).Where(h => h.ItemId == item.Id).ToList();
        var names = await catalog.GetMemberNamesAsync(holdings.Select(h => h.MemberId), ct);
        var stock = (await catalog.GetStockAsync([item.Id], ct))[item.Id];
        return Ok(new CostumeItemCardDto(item.ToDto(IsViewer), stock.InStore,
            holdings.Select(h => new CostumeHolderDto(names.ToParty(h.MemberId), h.Quantity)).ToList()));
    }

    // -------------------------------------------------------- předávky

    [HttpGet("transfers/{id:int}")]
    public async Task<ActionResult<CostumeTransferDto>> GetTransfer(int id, CancellationToken ct)
    {
        var transfer = await transfers.GetAsync(id, ct);
        if (transfer is null || !CanSee(transfer))
            return NotFound(new { error = "Předávka nebyla nalezena." });
        return Ok((await ToDtosAsync([transfer], ct)).Single());
    }

    [HttpGet("transfers/{id:int}/photos/{photoId:int}")]
    public async Task<IActionResult> GetPhoto(int id, int photoId, [FromQuery] string? size, CancellationToken ct)
    {
        var transfer = await transfers.GetAsync(id, ct);
        if (transfer is null || !CanSee(transfer))
            return NotFound(new { error = "Fotka nebyla nalezena." });

        var data = await transfers.GetPhotoDataAsync(id, photoId, thumbnail: size == "thumb", ct);
        return data is null ? NotFound(new { error = "Fotka nebyla nalezena." }) : File(data, "image/jpeg");
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<CostumeTransferDto>> CreateTransfer(
        [FromBody] CreateCostumeTransferRequest request, CancellationToken ct)
    {
        var toMemberId = request.ToMemberId;
        if (!string.IsNullOrWhiteSpace(request.ToQrToken))
        {
            var recipient = await qrService.FindMemberAsync(request.ToQrToken, ct);
            if (recipient is null)
                return BadRequest(new { error = "QR kód příjemce nepatří žádnému aktivnímu členovi." });
            toMemberId = recipient.Id;
        }

        var created = await transfers.CreateDraftAsync(Actor, request.FromMemberId, toMemberId,
            request.Lines.Select(l => new CostumeLineInput(l.Code, l.Quantity)).ToList(), request.Note, ct);
        if (!created.IsSuccess)
            return created.ToErrorResponse();

        var transfer = await transfers.GetAsync(created.Value, ct);
        return CreatedAtAction(nameof(GetTransfer), new { id = created.Value },
            (await ToDtosAsync([transfer!], ct)).Single());
    }

    [HttpPost("transfers/{id:int}/lines")]
    public async Task<IActionResult> AddLine(int id, [FromBody] CostumeLineRequest request, CancellationToken ct) =>
        ToResponse(await transfers.AddLineAsync(id, Actor, new CostumeLineInput(request.Code, request.Quantity), ct));

    [HttpDelete("transfers/{id:int}/lines/{lineId:int}")]
    public async Task<IActionResult> RemoveLine(int id, int lineId, CancellationToken ct) =>
        ToResponse(await transfers.RemoveLineAsync(id, lineId, Actor, ct));

    [HttpPut("transfers/{id:int}/lines/{lineId:int}/note")]
    public async Task<IActionResult> SetLineNote(int id, int lineId, [FromBody] CostumeLineNoteRequest request,
        CancellationToken ct) =>
        ToResponse(await transfers.SetLineNoteAsync(id, lineId, Actor, request.ConditionNote, ct));

    [HttpPost("transfers/{id:int}/lines/{lineId:int}/photos")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> AddPhoto(int id, int lineId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { error = "Nahraná fotka je prázdná." });

        await using var stream = file.OpenReadStream();
        return ToResponse(await transfers.AddPhotoAsync(id, lineId, Actor, stream, ct: ct));
    }

    [HttpDelete("transfers/{id:int}/photos/{photoId:int}")]
    public async Task<IActionResult> RemovePhoto(int id, int photoId, CancellationToken ct) =>
        ToResponse(await transfers.RemovePhotoAsync(id, photoId, Actor, ct));

    [HttpPost("transfers/{id:int}/submit")]
    public async Task<ActionResult<CostumeTransferStatusDto>> Submit(int id, CancellationToken ct)
    {
        var submitted = await transfers.SubmitAsync(id, Actor, ct);
        return submitted.IsSuccess
            ? Ok(new CostumeTransferStatusDto(id, submitted.Value.ToString()))
            : submitted.ToErrorResponse();
    }

    [HttpPost("transfers/{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct) =>
        ToResponse(await transfers.ConfirmAsync(id, Actor, ct));

    [HttpPost("transfers/{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectCostumeTransferRequest request, CancellationToken ct) =>
        ToResponse(await transfers.RejectAsync(id, Actor, request.Reason, ct));

    [HttpPost("transfers/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        ToResponse(await transfers.CancelAsync(id, Actor, ct));

    // ------------------------------------------- přehledy celé krojárky

    [HttpGet("catalog")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeViewers)]
    public async Task<ActionResult<CostumeCatalogDto>> GetCatalog(CancellationToken ct)
    {
        var costumes = await catalog.GetCostumesAsync(ct);
        var parts = await catalog.GetPartsAsync(ct);
        var stock = await catalog.GetStockAsync(ct: ct);
        return Ok(new CostumeCatalogDto(
            costumes.Select(c => c.ToDto()).ToList(),
            parts.Select(p => new CostumePartDto(p.Id, p.CostumeId, p.Name, p.Gender.ToString(), p.Description,
                p.TrackingMode.ToString(),
                p.Items.OrderBy(i => i.Code).Select(i =>
                {
                    var s = stock[i.Id];
                    return new CostumeStockItemDto(i.ToDto(includeValue: true), s.InStore, s.Held, s.Reserved);
                }).ToList())).ToList()));
    }

    [HttpGet("holders")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeViewers)]
    public async Task<ActionResult<List<CostumeHolderOverviewDto>>> GetHolders(CancellationToken ct)
    {
        var holdings = await catalog.GetHoldingsAsync(ct: ct);
        var names = await catalog.GetMemberNamesAsync(holdings.Select(h => h.MemberId), ct);
        return Ok(holdings.GroupBy(h => h.MemberId)
            .Select(g => new CostumeHolderOverviewDto(names.ToParty(g.Key),
                g.Select(h => new CostumeHoldingDto(h.Item.ToDto(includeValue: true), h.Quantity)).ToList()))
            .OrderBy(h => h.Member.FullName)
            .ToList());
    }

    [HttpGet("transfers/open")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeViewers)]
    public async Task<ActionResult<List<CostumeTransferDto>>> GetOpenTransfers(CancellationToken ct) =>
        Ok(await ToDtosAsync(await transfers.GetOpenAsync(ct), ct));

    [HttpGet("items/{id:int}/history")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeViewers)]
    public async Task<ActionResult<List<CostumeTransferDto>>> GetItemHistory(int id, CancellationToken ct) =>
        Ok(await ToDtosAsync(await transfers.GetItemHistoryAsync(id, ct), ct));

    // ------------------------------------------------ správa katalogu

    [HttpPost("costumes")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> CreateCostume([FromBody] CostumeRequest request, CancellationToken ct) =>
        ToResponse(await catalog.CreateCostumeAsync(new CostumeInput(request.Name, request.Description, request.SortOrder), ct));

    [HttpPut("costumes/{id:int}")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> UpdateCostume(int id, [FromBody] CostumeRequest request, CancellationToken ct) =>
        ToResponse(await catalog.UpdateCostumeAsync(id, new CostumeInput(request.Name, request.Description, request.SortOrder), ct));

    [HttpDelete("costumes/{id:int}")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> DeleteCostume(int id, CancellationToken ct) =>
        ToResponse(await catalog.DeleteCostumeAsync(id, ct));

    [HttpPost("parts")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> CreatePart([FromBody] CostumePartRequest request, CancellationToken ct) =>
        ToPartInput(request) is { } input
            ? ToResponse(await catalog.CreatePartAsync(input, ct))
            : BadRequest(new { error = "Neznámé pohlaví nebo režim evidence." });

    [HttpPut("parts/{id:int}")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> UpdatePart(int id, [FromBody] CostumePartRequest request, CancellationToken ct) =>
        ToPartInput(request) is { } input
            ? ToResponse(await catalog.UpdatePartAsync(id, input, ct))
            : BadRequest(new { error = "Neznámé pohlaví nebo režim evidence." });

    [HttpDelete("parts/{id:int}")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> DeletePart(int id, CancellationToken ct) =>
        ToResponse(await catalog.DeletePartAsync(id, ct));

    [HttpPost("items")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> CreateItem([FromBody] CostumeItemRequest request, CancellationToken ct) =>
        ToResponse(await catalog.CreateItemAsync(ToItemInput(request), ct));

    [HttpPut("items/{id:int}")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] CostumeItemRequest request, CancellationToken ct) =>
        ToResponse(await catalog.UpdateItemAsync(id, ToItemInput(request), ct));

    [HttpPut("items/{id:int}/state")]
    [Authorize(AuthenticationSchemes = Scheme, Roles = RoleNames.CostumeKeeper)]
    public async Task<IActionResult> SetItemState(int id, [FromBody] CostumeItemStateRequest request, CancellationToken ct) =>
        Enum.TryParse<CostumeItemState>(request.State, ignoreCase: true, out var state)
            ? ToResponse(await catalog.SetItemStateAsync(id, state, ct))
            : BadRequest(new { error = "Neznámý stav kusu." });

    // ------------------------------------------------------- pomocné

    private bool CanSee(CostumeTransfer transfer)
    {
        if (IsViewer)
            return true;
        var memberId = User.GetMemberId();
        return transfer.FromMemberId == memberId || transfer.ToMemberId == memberId || transfer.CreatedById == memberId;
    }

    private async Task<List<CostumeTransferDto>> ToDtosAsync(IReadOnlyCollection<CostumeTransfer> list, CancellationToken ct)
    {
        var names = await catalog.GetMemberNamesAsync(list.SelectMany(t => t.MemberIds()), ct);
        var result = new List<CostumeTransferDto>(list.Count);
        foreach (var transfer in list)
            result.Add(transfer.ToDto(names, await transfers.GetPhotoInfosAsync(transfer.Id, ct), IsViewer));
        return result;
    }

    private static CostumePartInput? ToPartInput(CostumePartRequest request) =>
        Enum.TryParse<CostumeGender>(request.Gender, ignoreCase: true, out var gender)
        && Enum.TryParse<CostumeTrackingMode>(request.TrackingMode, ignoreCase: true, out var mode)
            ? new CostumePartInput(request.CostumeId, request.Name, gender, request.Description, mode)
            : null;

    private static CostumeItemInput ToItemInput(CostumeItemRequest request) =>
        new(request.PartId, request.Code, request.Size, request.Color, request.Note, request.ValueCzk, request.Quantity);

    private IActionResult ToResponse(Result result) => result.IsSuccess ? NoContent() : result.ToErrorResponse();

    /// <summary>Vytvořené id jako <c>{ id }</c>, ať klient nemusí číst hlavičku Location.</summary>
    private IActionResult ToResponse(Result<int> result) =>
        result.IsSuccess ? Ok(new { id = result.Value }) : result.ToErrorResponse();
}
