namespace Demizon.Dal.Entities;

public class File
{
    public int Id { get; set; }
    
    public string Path { get; set; } = null!;

    public virtual Member? Member { get; set; }

    public int? MemberId { get; set; }
}
