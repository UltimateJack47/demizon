namespace Demizon.Dal.Entities;

public class DeviceToken
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    /// <summary>
    /// FCM registration token zaslaný mobilní aplikací.
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// Platforma zařízení: "android" nebo "ios".
    /// </summary>
    public string Platform { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public virtual Member Member { get; set; } = null!;
}
