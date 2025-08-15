namespace Domain.Entities;

public class Roadmap
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // قائمة الموارد المرتبة
    public ICollection<RoadmapResource> Resources { get; set; } = new List<RoadmapResource>();
}