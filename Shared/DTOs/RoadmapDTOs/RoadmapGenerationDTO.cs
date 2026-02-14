using System.Text.Json.Serialization;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapGenerationDTO
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("data")]
    public RoadmapSourcesDTO Data { get; set; } = new();

    [JsonPropertyName("learning_path")]
    public LearningPathDTO? LearningPath { get; set; }
}