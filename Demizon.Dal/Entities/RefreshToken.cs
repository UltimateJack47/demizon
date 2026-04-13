namespace Demizon.Dal.Entities;

public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>
    /// Hashovaný token – v DB nikdy neleží raw hodnota.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// Prvních 8 znaků raw tokenu (plaintext) – slouží jako DB index pro filtrování
    /// před bcrypt ověřením a odstraňuje O(n) scan všech aktivních tokenů.
    /// </summary>
    public string TokenPrefix { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int MemberId { get; set; }

    public virtual Member Member { get; set; } = null!;
}
