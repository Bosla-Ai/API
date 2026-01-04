namespace Domain.Entities;

public sealed class Roadmap
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public string UserId { get; set; }
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: roadmap has many courses via join
    public ICollection<RoadmapCourse> RoadmapCourses { get; set; } = new List<RoadmapCourse>();
}