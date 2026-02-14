using System.Text.Json.Serialization;

namespace Shared.DTOs.RoadmapDTOs;

public class LearningPathDTO
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = "";

    [JsonPropertyName("phases")]
    public List<LearningPhaseDTO> Phases { get; set; } = new();

    [JsonPropertyName("total_estimated_hours")]
    public double TotalEstimatedHours { get; set; }

    [JsonPropertyName("recommended_daily_hours")]
    public int RecommendedDailyHours { get; set; }

    [JsonPropertyName("estimated_completion_weeks")]
    public int EstimatedCompletionWeeks { get; set; }

    [JsonPropertyName("total_tags")]
    public int TotalTags { get; set; }

    [JsonPropertyName("recommended_prerequisites")]
    public List<string>? RecommendedPrerequisites { get; set; }
}

public class LearningPhaseDTO
{
    [JsonPropertyName("phase")]
    public int Phase { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<LearningTagDTO> Tags { get; set; } = new();

    [JsonPropertyName("estimated_hours")]
    public double EstimatedHours { get; set; }
}

public class LearningTagDTO
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";

    [JsonPropertyName("estimated_hours")]
    public double EstimatedHours { get; set; }

    [JsonPropertyName("prerequisites")]
    public List<string>? Prerequisites { get; set; }

    [JsonPropertyName("has_resource")]
    public bool HasResource { get; set; }

    [JsonPropertyName("resource_type")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("checkpoints")]
    public List<string>? Checkpoints { get; set; }
}
