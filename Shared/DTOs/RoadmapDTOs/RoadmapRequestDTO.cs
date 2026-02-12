using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapRequestDTO
{
    [JsonPropertyName("tags")]
    [Required(ErrorMessage = "At least one tag is required.")]
    [MaxLength(10, ErrorMessage = "Maximum 10 tags allowed.")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("prefer_paid")]
    public bool PreferPaid { get; set; } = false;

    [JsonPropertyName("language")]
    [MaxLength(10, ErrorMessage = "Language code must be 10 characters or less.")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("sources")]
    public string[]? Sources { get; set; }

    [JsonPropertyName("tag_checkpoints")]
    public Dictionary<string, string[]>? TagCheckpoints { get; set; }

    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }
}