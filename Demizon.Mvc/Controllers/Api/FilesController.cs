using Demizon.Contracts.Gallery;
using Demizon.Core.Services.File;
using Demizon.Core.Services.FileUpload;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/files")]
public class FilesController(IFileService fileService) : ControllerBase
{
    [HttpGet("{id:int}/image")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetImage(int id, [FromQuery] string size = "full")
    {
        try
        {
            var file = await fileService.GetOneAsync(id);

            byte[]? data = size == "thumb" ? file.ThumbnailData ?? file.Data : file.Data;
            if (data == null || data.Length == 0)
                return NotFound();

            return File(data, file.ContentType);
        }
        catch (Common.Exceptions.EntityNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("gallery")]
    public async Task<ActionResult<List<GalleryPhotoDto>>> GetGalleryPhotos()
    {
        var photos = await fileService.GetAll()
            .Where(f => f.IsPublic && f.Data != null)
            .Include(f => f.Dance)
            .OrderByDescending(f => f.Id)
            .Select(f => new GalleryPhotoDto(f.Id, f.Dance != null ? f.Dance.Name : null))
            .ToListAsync();

        return Ok(photos);
    }
}

[ApiController]
[Route("api/gallery")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class GalleryController(IFileService fileService, IFileUploadService fileUploadService) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.ContentType.StartsWith("image/"))
            return BadRequest("Only image files are allowed.");

        await using var stream = file.OpenReadStream();
        var result = await fileUploadService.UploadImageToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileExtension = Path.GetExtension(file.FileName),
            FileName = Path.GetFileNameWithoutExtension(file.FileName),
            FileSize = file.Length,
            ContentType = file.ContentType
        });

        if (!result.IsSuccessful)
            return StatusCode(500, "Upload failed.");

        var entity = new Dal.Entities.File
        {
            Path = result.RelativePath,
            FileExtension = result.FileExtension,
            ContentType = result.ContentType,
            FileSize = result.FileSize,
            Data = result.Data,
            ThumbnailData = result.ThumbnailData,
            IsPublic = true
        };

        await fileService.CreateAsync(entity);
        return Ok(new { entity.Id });
    }
}
