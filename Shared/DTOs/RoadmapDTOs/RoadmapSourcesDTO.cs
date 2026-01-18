using System.Text.Json.Serialization;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapSourcesDTO
{
    [JsonPropertyName("youtube")]
    public Dictionary<string, RoadmapItemDTO?> Youtube { get; set; } = new();

    [JsonPropertyName("coursera")]
    public Dictionary<string, RoadmapItemDTO?> Coursera { get; set; } = new();

    [JsonPropertyName("udemy")]
    public Dictionary<string, RoadmapItemDTO?> Udemy { get; set; } = new();
}