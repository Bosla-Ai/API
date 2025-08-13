namespace Domain.Entities;

public sealed class Roadmap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; }
    public string Description { get; set; }

    public Guid ApplicationUserId { get; set; }
    public Customer Customer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // قائمة الموارد المرتبة
    public ICollection<RoadmapResource> Resources { get; set; } = new List<RoadmapResource>();
}