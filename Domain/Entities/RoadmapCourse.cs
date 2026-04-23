namespace Domain.Entities;

public sealed class RoadmapCourse
{
    public int RoadmapId { get; set; }
    public Roadmap? Roadmap { get; set; }

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public int Order { get; set; }  // optional: ordering inside roadmap
    public string? SectionName { get; set; }

    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    // Video progress tracking
    public int CurrentPositionSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string? VideoId { get; set; }
}