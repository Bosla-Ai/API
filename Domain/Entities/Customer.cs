using Shared.Enums;

namespace Domain.Entities;

public class Customer 
{
    public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    
    // For Option 1: Manual Input
    public LevelType? UserLevel { get; set; }
    public string? PreferredFramework { get; set; } // "React", "Angular", "Vue", etc.
    public TrackCategory? SelectedTrack { get; set; }
    public bool WantsDeepDive { get; set; } = false;
    public int? AvailableHoursPerWeek { get; set; }
    
    // For Option 2: CV Analysis Results
    public string? AnalyzedSkillsJson { get; set; } // JSON from LLM analysis
    public string? ExperienceLevelJson { get; set; }
    public string? RecommendedTracksJson { get; set; }
    public DateTime? LastCvAnalysisDate { get; set; }
    
    // Computed Learning Score (for recommendation algorithm)
    public double LearningScore { get; set; } = 0.0; // Based on level, hours, reviews, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}