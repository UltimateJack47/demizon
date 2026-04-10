namespace Demizon.Dal.Entities;

public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>
    /// Hashovaný token – v DB nikdy neleží raw hodnota.
    /// </summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int MemberId { get; set; }

    public virtual Member Member { get; set; } = null!;
}
