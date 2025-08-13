namespace Domain.Entities;

public sealed class RoadmapResource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoadmapId { get; set; }
    public Roadmap Roadmap { get; set; }

    public int ResourceId { get; set; }
    public Resource Resource { get; set; }

    public int Order { get; set; }

    public string Notes { get; set; }
}
