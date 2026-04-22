using Shared.Enums;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapCourseResponseDTO
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Instructor { get; set; }
    public Platforms Platform { get; set; }
    public string? ImageUrl { get; set; }
    public string? Duration { get; set; }
    public double Rating { get; set; }
    public ResourceLanguage Language { get; set; }
    public BudgetPreference? CourseBudget { get; set; }

    public int Order { get; set; }
    public string? SectionName { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CurrentPositionSeconds { get; set; }
    public int TotalDurationSeconds { get; set; }
    public string? VideoId { get; set; }
}
