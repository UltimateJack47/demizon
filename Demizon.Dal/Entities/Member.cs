namespace Demizon.Dal.Entities;

public class Member
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;
    
    public string LastName { get; set; } = null!;

    public Gender Gender { get; set; }

    public bool IsVisible { get; set; }

    public DateTime? BirthDate { get; set; }
    
    public DateTime? MemberSince { get; set; }

    public virtual List<File> Photos { get; set; } = new List<File>();
}

public enum Gender
{
    Male,
    Female
}
