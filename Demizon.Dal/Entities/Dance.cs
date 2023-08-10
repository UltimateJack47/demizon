namespace Demizon.Dal.Entities;

public class Dance
{
    public int Id { get; set; }
    
    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;
}
