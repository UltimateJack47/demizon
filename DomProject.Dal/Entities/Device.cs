namespace DomProject.Dal.Entities;

public class Device
{
    public int Id { get; set; }
    
    public string Name { get; set; } = null!;

    public int Year { get; set; }

    public decimal Price { get; set; }

    public string Description { get; set; } = null!;
}
