namespace Shared.DTOs.RoadmapDTOs;

public class CourseProgressUpdateDTO
{
    public int CurrentPositionSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string? VideoId { get; set; }
    public bool IsCompleted { get; set; }
}
