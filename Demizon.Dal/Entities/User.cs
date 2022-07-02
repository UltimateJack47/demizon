namespace DomProject.Dal.Entities;

public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public virtual IList<Borrowing> Borrowings { get; set; } = new List<Borrowing>();
}
