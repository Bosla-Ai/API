using Shared.Enums;

namespace Domain.Entities;

public sealed class Roadmap
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public RoadmapSourceType SourceType { get; set; }
    public string? TargetJobRole { get; set; }
    public string? GenerationId { get; set; }

    // relationships
    public string CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<RoadmapCourse> RoadmapCourses { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; } = false;
}