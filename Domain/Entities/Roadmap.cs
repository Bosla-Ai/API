namespace Domain.Entities;

public class Roadmap : BaseAuditEntity
{
    public string Title { get; set; }
    public string Description { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    // قائمة الموارد المرتبة
    public ICollection<RoadmapResource> Resources { get; set; } = new List<RoadmapResource>();
}