namespace Demizon.Dal.Entities;

public class Member
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public Gender Gender { get; set; }

    public bool IsVisible { get; set; } = false;

    public bool IsAttendanceVisible { get; set; } = true;

    public bool IsDancer { get; set; } = false;

    public bool IsMusician { get; set; } = false;

    public bool IsExternal { get; set; } = false;

    public DateTime? BirthDate { get; set; }

    public DateTime? MemberSince { get; set; }

    /// <summary>
    /// Oprávnění člena. Prázdný seznam = běžný člen. Co člověk v souboru dělá
    /// (<see cref="IsDancer"/>, <see cref="IsMusician"/>) role nejsou a sem nepatří.
    /// </summary>
    public virtual List<MemberRole> Roles { get; set; } = [];

    public virtual List<File> Photos { get; set; } = [];

    public virtual List<Attendance> Attendances { get; set; } = [];

    public virtual List<PushSubscription> PushSubscriptions { get; set; } = [];

    public virtual List<RefreshToken> RefreshTokens { get; set; } = [];

    public virtual List<DeviceToken> DeviceTokens { get; set; } = [];

    /// <summary>
    /// Soft delete – null = aktivní člen. Záznamy s hodnotou jsou filtrovány global query filtrem.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// OAuth refresh token pro Google Calendar. Null = nepropojeno.
    /// </summary>
    public string? GoogleRefreshToken { get; set; }

    /// <summary>
    /// ID Google Calendar, do kterého se zapisují události (obvykle "primary").
    /// </summary>
    public string? GoogleCalendarId { get; set; }

    /// <summary>
    /// UTC čas, kdy bylo propojení s Google Calendar navázáno.
    /// </summary>
    public DateTime? GoogleConnectedAt { get; set; }

    public bool HasRole(UserRole role) => Roles.Any(r => r.Role == role);
}

/// <summary>
/// Ukládá se jako text, číselné hodnoty nic neznamenají. Běžný člen žádnou roli nemá.
/// </summary>
public enum UserRole
{
    Admin = 1,
    /// <summary>Spravuje obsah veřejného webu (tance, videa, galerie), ne členy ani docházku.</summary>
    ContentManager = 2
}

public enum Gender
{
    Male,
    Female
}