using CryptoHelper;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services.Authentication;

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
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return rawToken;
    }

    /// <summary>
    /// Ověří raw token – vrátí MemberId pokud je platný, jinak null.
    /// </summary>
    public async Task<int?> ValidateAsync(string rawToken)
    {
        // Musíme iterovat platné tokeny tohoto časového okna – hash musíme ověřit Crypto.VerifyHashedPassword
        var candidates = await db.RefreshTokens
            .Where(t => !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        var match = candidates.FirstOrDefault(t => Crypto.VerifyHashedPassword(t.TokenHash, rawToken));

        if (match is null)
            return null;

        // Revokujeme použitý token (token rotation – každé použití vydá nový)
        match.IsRevoked = true;
        await db.SaveChangesAsync();

        return match.MemberId;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[TokenByteLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
