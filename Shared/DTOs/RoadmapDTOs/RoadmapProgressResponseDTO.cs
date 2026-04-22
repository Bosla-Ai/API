namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapProgressResponseDTO
{
    public int RoadmapId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalCourses { get; set; }
    public int CompletedCourses { get; set; }
    public double CompletionPercent { get; set; }
    public List<CourseProgressDTO> Courses { get; set; } = [];
}

public class CourseProgressDTO
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int CurrentPositionSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string? VideoId { get; set; }
    public double WatchPercent { get; set; }
}
