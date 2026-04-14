using CryptoHelper;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Authentication;

/// <summary>
/// Spravuje refresh tokeny – generování, validaci a rotaci.
/// </summary>
public class RefreshTokenService(DemizonContext db)
{
    private const int TokenByteLength = 64;

    /// <summary>
    /// Vygeneruje nový refresh token, uloží jeho hash do DB a vrátí raw hodnotu pro klienta.
    /// Operace revokace + vytvoření je atomická díky transakci.
    /// </summary>
    public async Task<string> CreateAsync(int memberId, int expirationDays)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // Odvoláme staré tokeny tohoto člena (jen platné)
        var oldTokens = await db.RefreshTokens
            .Where(t => t.MemberId == memberId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var old in oldTokens)
            old.IsRevoked = true;

        var rawToken = GenerateSecureToken();
        var tokenHash = Crypto.HashPassword(rawToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            MemberId = memberId,
            TokenHash = tokenHash,
            TokenPrefix = rawToken[..8],
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return rawToken;
    }

    /// <summary>
    /// Ověří raw token – vrátí MemberId pokud je platný, jinak null.
    /// Filtruje přes TokenPrefix index v DB, bcrypt ověření probíhá jen na 1–2 kandidátech.
    /// Revokace je atomická podmíněným UPDATE (WHERE IsRevoked = 0) — zabraňuje replay útoku
    /// při souběžných požadavcích se stejným tokenem.
    /// </summary>
    public async Task<int?> ValidateAsync(string? rawToken)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length < 8)
            return null;

        var prefix = rawToken[..8];
        var candidates = await db.RefreshTokens
            .Where(t => !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow && t.TokenPrefix == prefix)
            .ToListAsync();

        var match = candidates.FirstOrDefault(t => Crypto.VerifyHashedPassword(t.TokenHash, rawToken));

        if (match is null)
            return null;

        // Atomicky revokujeme token podmíněným UPDATE — vrátí 0 pokud již byl revokován
        // souběžným požadavkem (token rotation replay ochrana).
        var updated = await db.RefreshTokens
            .Where(t => t.Id == match.Id && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));

        if (updated == 0)
            return null;

        return match.MemberId;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[TokenByteLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
