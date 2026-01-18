using Shared.Enums;

namespace Domain.Entities;

public sealed class Course
{
    public int Id { get; set; }
    // Core Info
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Url { get; set; } = ""; // Unique Index recommended here
    public string? Instructor { get; set; }
    public Platforms Platform { get; set; }  // e.g. Coursera, edX, Udemy

    public string? ImageUrl { get; set; }
    public string? Duration { get; set; }   // e.g. "12.5 Hours"
    public double Rating { get; set; }      // e.g. 4.8
    public int ReviewCount { get; set; }    // e.g. 1500

    public string Language { get; set; } = "en"; // ar, en, es, fr, de, etc.

    public BudgetPreference? CourseBudget { get; set; } = BudgetPreference.Free;
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<RoadmapCourse> RoadmapCourses { get; set; } = new List<RoadmapCourse>();
    public ICollection<CourseTag> CourseTags { get; set; } = new List<CourseTag>();
}