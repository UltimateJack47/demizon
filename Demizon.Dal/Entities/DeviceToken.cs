namespace Demizon.Dal.Entities;

public enum DevicePlatform
{
    Android,
    Ios
}

public class DeviceToken
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    /// <summary>
    /// FCM registration token zaslaný mobilní aplikací.
    /// </summary>
    public string Token { get; set; } = null!;

    public DevicePlatform Platform { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public virtual Member Member { get; set; } = null!;
}
