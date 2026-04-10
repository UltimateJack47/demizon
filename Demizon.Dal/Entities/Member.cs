namespace Demizon.Dal.Entities;

public class Member
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Standard;

    public Gender Gender { get; set; }

    public bool IsVisible { get; set; } = false;

    public bool IsAttendanceVisible { get; set; } = true;

    public DateTime? BirthDate { get; set; }

    public DateTime? MemberSince { get; set; }

    public virtual List<File> Photos { get; set; } = [];

    public virtual List<Attendance> Attendances { get; set; } = [];

    public virtual List<PushSubscription> PushSubscriptions { get; set; } = [];

    public virtual List<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>
    /// Soft delete – null = aktivní člen. Záznamy s hodnotou jsou filtrovány global query filtrem.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}

public enum UserRole
{
    Standard = 0,
    Admin = 1
}

public enum Gender
{
    Male,
    Female
}