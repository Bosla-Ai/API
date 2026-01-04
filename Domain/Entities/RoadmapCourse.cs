namespace Domain.Entities;

public sealed class RoadmapCourse
{
    public int RoadmapId { get; set; }
    public Roadmap? Roadmap { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public int Order { get; set; } = 0; // optional: ordering inside roadmap
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}