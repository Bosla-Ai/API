using Shared.Enums;

namespace Domain.Entities;

public class Resource
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Url { get; set; }
    public ResourceType ResourceType { get; set; }
    public LevelType Level { get; set; }
    public int DurationMinutes { get; set; }
    public double PopularityScore { get; set; } = 0.0; // normalized 0..1
    public string Provider { get; set; }
    public bool HasLab { get; set; } = false; // useful for Cyber/ML

    public Guid TopicId { set; get; }
}
