using Shared.Enums;

namespace Domain.Entities;

public sealed class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Instructor { get; set; }
    public string Language { get; set; }
    public LevelType Difficulty { get; set; }
    public Platforms Platform { get; set; }  // e.g. Coursera, edX, Udemy

    public string Url { get; set; } = "";
    public BudgetPreference? CourseBudget { get; set; } = BudgetPreference.Free;
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<RoadmapCourse> RoadmapCourses { get; set; } = new List<RoadmapCourse>();
    public ICollection<CourseTag> CourseTags { get; set; } = new List<CourseTag>();
}