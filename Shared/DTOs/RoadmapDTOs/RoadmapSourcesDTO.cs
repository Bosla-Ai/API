using System.Text.Json.Serialization;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapSourcesDTO
{
    [JsonPropertyName("youtube")]
    public Dictionary<string, RoadmapItemDTO?> Youtube { get; set; } = [];

    [JsonPropertyName("coursera")]
    public Dictionary<string, RoadmapItemDTO?> Coursera { get; set; } = [];

    [JsonPropertyName("udemy")]
    public Dictionary<string, RoadmapItemDTO?> Udemy { get; set; } = [];
}