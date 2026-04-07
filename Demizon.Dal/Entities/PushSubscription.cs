namespace Demizon.Dal.Entities;

/// <summary>
/// Uloží Web Push subscription endpoint pro daného člena.
/// Jeden člen může mít více subscriptions (různá zařízení / prohlížeče).
/// </summary>
public class PushSubscription
{
    public int Id { get; set; }

    public int MemberId { get; set; }
    public virtual Member Member { get; set; } = null!;

    /// <summary>Web Push endpoint URL poskytnutý prohlížečem.</summary>
    public string Endpoint { get; set; } = null!;

    /// <summary>ECDH public key (base64url) – P-256 křivka.</summary>
    public string P256dh { get; set; } = null!;

    /// <summary>Authentication secret (base64url) – 16 bytů.</summary>
    public string Auth { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
