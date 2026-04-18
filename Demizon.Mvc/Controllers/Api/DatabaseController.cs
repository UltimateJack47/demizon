using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Demizon.Dal;
using Demizon.Dal.Entities;
using CryptoHelper;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Controllers.Api;

/// <summary>
/// Endpoint pro backup/restore databáze a seed pro inicializaci.
/// </summary>
[ApiController]
[Route("api/database")]
public class DatabaseController(ILogger<DatabaseController> logger, DemizonContext dbContext) : ControllerBase
{
    private const string DatabasePath = "/data/demizon.sqlite";

    /// <summary>
    /// Naplní prázdnou DB testem pro inicializaci.
    /// POST /api/database/seed
    /// Dostupné bez loginu (jen pro empty DB!)
    /// </summary>
    [HttpPost("seed")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedDatabase()
    {
        try
        {
            // Kontrola: pouze pokud je DB prázdná
            if (await dbContext.Members.AnyAsync())
            {
                return BadRequest("Database is not empty. Seed is only for initialization.");
            }

            // Vytvoř testovacího člena
            var testMember = new Member
            {
                Name = "Admin",
                Surname = "Test",
                Email = "admin@demizon.local",
                Login = "jack",
                PasswordHash = Crypto.HashPassword("testpass"),
                Role = UserRole.Admin,
                DeletedAt = null
            };

            dbContext.Members.Add(testMember);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Database seeded with test member: admin@demizon.local / admin123");
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

    /// <summary>
    /// Stáhne aktuální SQLite databázi jako ZIP soubor.
    /// GET /api/database/backup
    /// </summary>
    [HttpGet("backup")]
    [Authorize]
    public async Task<IActionResult> DownloadBackup()
    {
        try
        {
            if (!System.IO.File.Exists(DatabasePath))
            {
                return NotFound("Database file not found");
            }

            var zipPath = $"/tmp/demizon-backup-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.zip";
            using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                // Přidej hlavní DB soubor
                zip.CreateEntryFromFile(DatabasePath, "demizon.sqlite");
                
                // Přidej WAL a SHM soubory pokud existují
                var walPath = $"{DatabasePath}-wal";
                var shmPath = $"{DatabasePath}-shm";
                if (System.IO.File.Exists(walPath))
                    zip.CreateEntryFromFile(walPath, "demizon.sqlite-wal");
                if (System.IO.File.Exists(shmPath))
                    zip.CreateEntryFromFile(shmPath, "demizon.sqlite-shm");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            System.IO.File.Delete(zipPath); // Vyčistit po sobě

            return File(fileBytes, "application/zip", $"demizon-backup-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.zip");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database backup failed");
            return StatusCode(500, new { error = "Backup failed", details = ex.Message });
        }
    }

    [HttpGet("info")]
    [Authorize]
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
