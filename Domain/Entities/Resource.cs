using Shared.Enums;

namespace Domain.Entities;

public class Resource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; }
    public ResourceType Type { get; set; } // Video, Book, Article, Tutorial, Documentation
    public LevelType Level { get; set; }
    public string Tags { get; set; } = "[]"; // JSON array as string
    public int DurationMinutes { get; set; }
    public double PopularityScore { get; set; } = 0.0; // Based on views/usage
    public double QualityScore { get; set; } = 0.0; // Based on user reviews (0-5)
    public int ReviewCount { get; set; } = 0;
    public string Language { get; set; } = "English";
    public string Provider { get; set; } // "YouTube", "MDN", "FreeCodeCamp", "GitHub"
    public bool IsFree { get; set; } = true;
    public string? Framework { get; set; } // "React", "Angular", "Vue", etc.
    public TrackCategory? Track { get; set; } // Which track this resource belongs to
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<ResourceTag> ResourceTags { get; set; } = new List<ResourceTag>();
}