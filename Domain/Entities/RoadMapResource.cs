namespace Domain.Entities;

public sealed class RoadmapResource // Third Table Many-Many Relationship
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoadmapId { get; set; }
    // nav properity from RoadMap
    public Roadmap Roadmap { get; set; }

    public int ResourceId { get; set; }
    // nav properity from Resource 
    public Resource Resource { get; set; }

    public int Order { get; set; }

    public string Notes { get; set; }
}
