using Demizon.Contracts.Dances;
using Demizon.Contracts.Gallery;
using Demizon.Core.Services.Dance;
using Demizon.Core.Services.File;
using Demizon.Core.Services.FileUpload;
using Demizon.Dal.Entities;
using Demizon.Mvc.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/dances")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DancesController(IDanceService danceService, IFileService fileService, IFileUploadService fileUploadService) : ControllerBase
{
    private static readonly string[] AllowedDocumentContentTypes =
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private static readonly string[] AllowedDocumentExtensions = { ".pdf", ".doc", ".docx" };

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

    [HttpGet("{id:int}/photos")]
    public async Task<ActionResult<List<GalleryPhotoDto>>> GetDancePhotos(int id)
    {
        var photos = await fileService.GetAll()
            .Where(f => f.DanceId == id && f.Kind == FileKind.Image && f.Data != null)
            .OrderByDescending(f => f.Id)
            .Select(f => new GalleryPhotoDto(f.Id, null))
            .ToListAsync();

        return Ok(photos);
    }

    [HttpGet("{id:int}/documents")]
    public async Task<ActionResult<List<DanceDocumentDto>>> GetDanceDocuments(int id)
    {
        var docs = await fileService.GetAll()
            .Where(f => f.DanceId == id && f.Kind == FileKind.Document)
            .OrderBy(f => f.Id)
            .ToListAsync();

        return Ok(docs.Select(f => f.ToDocumentDto()).ToList());
    }

    [HttpPost("{id:int}/documents")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<DanceDocumentDto>> UploadDanceDocument(int id, IFormFile file)
    {
        try
        {
            _ = await danceService.GetOneAsync(id);
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }

        if (file.Length == 0)
            return BadRequest("No file provided.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedDocumentExtensions.Contains(extension) ||
            !AllowedDocumentContentTypes.Contains(file.ContentType))
        {
            return BadRequest("Only PDF and Word documents are allowed.");
        }

        await using var stream = file.OpenReadStream();
        var result = await fileUploadService.UploadDocumentToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileExtension = extension,
            FileName = file.FileName,
            FileSize = file.Length,
            ContentType = file.ContentType
        });

        if (!result.IsSuccessful)
            return StatusCode(500, "Upload failed.");

        var entity = new Dal.Entities.File
        {
            DanceId = id,
            Path = file.FileName,
            FileExtension = result.FileExtension,
            ContentType = result.ContentType,
            FileSize = result.FileSize,
            Data = result.Data,
            ThumbnailData = null,
            IsPublic = false,
            Kind = FileKind.Document
        };

        await fileService.CreateAsync(entity);
        return Ok(entity.ToDocumentDto());
    }

    [HttpDelete("{id:int}/documents/{fileId:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<IActionResult> DeleteDanceDocument(int id, int fileId)
    {
        try
        {
            var file = await fileService.GetOneAsync(fileId);
            if (file.DanceId != id || file.Kind != FileKind.Document)
                return NotFound();

            await fileService.DeleteAsync(fileId);
            return NoContent();
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }
}
