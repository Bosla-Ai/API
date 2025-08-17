namespace Domain.Entities;

public class RoadmapResource : BaseEntity
{
    public int RoadmapId { get; set; }
    public Roadmap Roadmap { get; set; }

    public int ResourceId { get; set; }
    public Resource Resource { get; set; }

    // ترتيب Resource داخل الـ Roadmap
    public int Order { get; set; }

    // optional: note per resource
    public string Notes { get; set; }
}