using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CryptoHelper;
using Demizon.Common.Configuration;
using Demizon.Contracts.Auth;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Demizon.Mvc.Controllers.Api;

/// <summary>
/// Jednorázový bootstrap prvního admina a info o souboru databáze.
/// Zálohy volume řeší Scaleway, ne tento host.
/// </summary>
[ApiController]
[Route("api/database")]
public class DatabaseController(
    ILogger<DatabaseController> logger,
    DemizonContext dbContext,
    IOptions<BootstrapSettings> bootstrapOptions) : ControllerBase
{
    private const string DatabasePath = "/data/demizon.sqlite";
    private const string SeedTokenHeader = "X-Seed-Token";

    /// <summary>
    /// Založí prvního admina. Trojitá pojistka: bez nakonfigurovaného
    /// <c>Bootstrap:SeedToken</c> endpoint neexistuje (404), se špatným tokenem
    /// vrací 401 a nad neprázdnou tabulkou členů 409. Po prvním úspěchu se tím
    /// sám vypne — proto „jednorázový“.
    /// </summary>
    [HttpPost("seed")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SeedAdmin([FromBody] SeedAdminRequest request)
    {
        var expectedToken = bootstrapOptions.Value.SeedToken;
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            // Nenakonfigurováno = feature je vypnutá. 404 místo 403, aby se
            // z odpovědi nedalo poznat, že tady vůbec něco takového je.
            logger.LogWarning("Seed endpoint byl zavolán, ale Bootstrap:SeedToken není nastaven.");
            return NotFound();
        }

        if (!Request.Headers.TryGetValue(SeedTokenHeader, out var provided)
            || !TokenMatches(provided.ToString(), expectedToken))
        {
            logger.LogWarning("Seed endpoint: neplatný {Header}.", SeedTokenHeader);
            return Unauthorized(new { error = $"Invalid {SeedTokenHeader}." });
        }

        if (await dbContext.Members.IgnoreQueryFilters().AnyAsync())
        {
            // IgnoreQueryFilters: soft-smazaný člen taky drží login a hash,
            // takže „prázdná databáze“ musí znamenat i žádný smazaný.
            return Conflict(new { error = "Database already has members. Seed is for initialization only." });
        }

        var admin = new Member
        {
            Name = request.Name,
            Surname = request.Surname,
            Email = request.Email,
            Login = request.Login,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            Role = UserRole.Admin,
            DeletedAt = null
        };

        dbContext.Members.Add(admin);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("První admin {Login} založen přes seed endpoint.", admin.Login);

        // Heslo se v odpovědi nevrací — zná ho ten, kdo request poslal.
        return Ok(new { id = admin.Id, login = admin.Login, role = admin.Role.ToString() });
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
            // Detail výjimky jde do logu, ne do odpovědi — nese cesty na disku.
            logger.LogError(ex, "Failed to get database info");
            return StatusCode(500, new { error = "Failed to get info" });
        }
    }

    /// <summary>
    /// Srovnání v konstantním čase, aby se token nedal uhádat po znacích.
    /// Rozdílná délka vrací false bez porovnávání.
    /// </summary>
    private static bool TokenMatches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
}
