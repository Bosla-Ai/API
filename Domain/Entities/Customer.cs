using Shared.Enums;

namespace Domain.Entities;

public sealed class Customer 
{
    public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    
    // For Option 1: Manual Input
    public LevelType? UserLevel { get; set; }
    public string? PreferredFramework { get; set; } // "React", "Angular", "Vue", etc.
    public Domains? SelectedDomain { get; set; }
    public bool WantsDeepDive { get; set; } = false;
    public int? AvailableHoursPerWeek { get; set; }
    public BudgetPreference? BudgetPreference { get; set; } = Shared.Enums.BudgetPreference.Free;
    public DateTime? TargetCompletionDate { get; set; }
    
    // For Option 2: CV Analysis Results
    public string? AnalyzedSkillsJson { get; set; } // JSON from LLM analysis
    public string? ExperienceLevelJson { get; set; }
    public string? RecommendedTracksJson { get; set; }
    public DateTime? LastCvAnalysisDate { get; set; }
    
    // Computed Learning Score (for recommendation algorithm)
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Roadmap> RoadMaps { get; set; } = new List<Roadmap>();
}