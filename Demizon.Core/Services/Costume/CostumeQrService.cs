using System.Security.Cryptography;
using Demizon.Common;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Costume;

/// <summary>
/// QR z profilu člena. Token jen říká, komu správce předává — nic nepovoluje, předávku potvrzuje
/// přihlášený příjemce. Přegenerováním přestane platit kopie, která se někde válí.
/// </summary>
public sealed class CostumeQrService(DemizonContext db, ILogger<CostumeQrService> logger)
{
    /// <summary>96 bitů náhody; 16 znaků base64url drží QR malý a čitelný i z mobilu.</summary>
    private const int TokenBytes = 12;

    public async Task<Result<string>> GetOrCreateTokenAsync(int memberId, CancellationToken ct = default)
    {
        var member = await db.Members.FindAsync([memberId], ct);
        if (member is null)
            return Result<string>.NotFound("Člen nebyl nalezen.");
        if (member.CostumeQrToken is { } existing)
            return Result<string>.Ok(existing);

        return await RegenerateAsync(member, ct);
    }

    public async Task<Result<string>> RegenerateTokenAsync(int memberId, CancellationToken ct = default)
    {
        var member = await db.Members.FindAsync([memberId], ct);
        return member is null
            ? Result<string>.NotFound("Člen nebyl nalezen.")
            : await RegenerateAsync(member, ct);
    }

    /// <summary>Aktivní člen, který se umí přihlásit (externista ne — předávku by nepotvrdil).</summary>
    public async Task<Dal.Entities.Member?> FindMemberAsync(string token, CancellationToken ct = default)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
            return null;
        return await db.Members.FirstOrDefaultAsync(m => m.CostumeQrToken == trimmed && !m.IsExternal, ct);
    }

    private async Task<Result<string>> RegenerateAsync(Dal.Entities.Member member, CancellationToken ct)
    {
        try
        {
            member.CostumeQrToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            await db.SaveChangesAsync(ct);
            return Result<string>.Ok(member.CostumeQrToken);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to generate costume QR token for member {MemberId}.", member.Id);
            return Result<string>.Fail("QR kód se nepodařilo vytvořit.");
        }
    }
}
