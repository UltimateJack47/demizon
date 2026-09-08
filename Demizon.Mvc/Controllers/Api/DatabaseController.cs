using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Demizon.Dal;
using Demizon.Dal.Entities;
using CryptoHelper;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

/// <summary>
/// Seed initialization and database file info. Volume backups live in Scaleway, not here.
/// </summary>
[ApiController]
[Route("api/database")]
public class DatabaseController(ILogger<DatabaseController> logger, DemizonContext dbContext) : ControllerBase
{
    private const string DatabasePath = "/data/demizon.sqlite";

    [HttpPost("seed")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedDatabase()
    {
        try
        {
            if (await dbContext.Members.AnyAsync())
            {
                return BadRequest("Database is not empty. Seed is only for initialization.");
            }

            var testMember = new Member
            {
                Name = "Admin",
                Surname = "Test",
                Email = "admin@demizon.local",
                Login = "jack",
                PasswordHash = PasswordHasher.HashPassword("admin123"),
                Role = UserRole.Admin,
                DeletedAt = null
            };

            dbContext.Members.Add(testMember);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Database seeded with test member");
            return Ok(new
            {
                message = "Database seeded successfully",
                testUser = new
                {
                    email = "admin@demizon.local",
                    password = "admin123"
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed");
            return StatusCode(500, new { error = "Seeding failed", details = ex.Message });
        }
    }

    [HttpGet("info")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetDatabaseInfo()
    {
        try
        {
            if (!System.IO.File.Exists(DatabasePath))
            {
                return NotFound("Database file not found");
            }

            var fileInfo = new System.IO.FileInfo(DatabasePath);
            return Ok(new
            {
                path = DatabasePath,
                sizeBytes = fileInfo.Length,
                sizeKb = fileInfo.Length / 1024,
                lastModified = fileInfo.LastWriteTimeUtc,
                exists = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get database info");
            return StatusCode(500, new { error = "Failed to get info", details = ex.Message });
        }
    }
}
