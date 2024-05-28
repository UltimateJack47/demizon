namespace Demizon.Dal.Entities;

public class User
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

    public DateTime? BirthDate { get; set; }
    
    public DateTime? MemberSince { get; set; }

    public virtual List<File> Photos { get; set; } = [];
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
