using System.Text.Json.Serialization;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapRequestDTO
{
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("level")]
    public string Level { get; set; } = "beginner";

    [JsonPropertyName("prefer_paid")]
    public bool PreferPaid { get; set; } = false;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";
}