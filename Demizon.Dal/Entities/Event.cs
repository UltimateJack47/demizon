namespace DomProject.Dal.Entities;

public class Event
{
    public int Id { get; set; }
    
    public string Name { get; set; } = null!;

    public DateTime Date { get; set; }

    public string? Place { get; set; }
}
