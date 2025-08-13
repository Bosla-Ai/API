using Shared.Enums;

namespace Domain.Entities;

public sealed class RoadMap
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string GeneratedContent { get; set; } // LLM formatted roadmap
    public string UserInputAsJson { get; set; } // Original user input
    public string RecommendedResourcesJson { get; set; } = "[]"; // IDs of recommended resources
    
    // Metadata
    public RoadMapGenerationType GenerationType { get; set; } // Manual vs CV_Analysis
    public LevelType StartingLevel { get; set; }
    public TrackCategory Track { get; set; }
    public string? Framework { get; set; }
    public bool IsDeepDive { get; set; } = false;
    
    // Relationships
    public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    
    // Timestamps
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}