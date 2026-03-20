using Newtonsoft.Json;

namespace Shared.DTOs;

public class UserProfileEntity
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("interests")]
    public List<string>? Interests { get; set; }

    [JsonProperty("experienceLevel")]
    public string? ExperienceLevel { get; set; }

    [JsonProperty("targetRole")]
    public string? TargetRole { get; set; }

    [JsonProperty("constraints")]
    public List<string>? Constraints { get; set; }

    [JsonProperty("personalityHints")]
    public List<string>? PersonalityHints { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("extractionCount")]
    public int ExtractionCount { get; set; } = 0;

    public void MergeFrom(UserProfileEntity newer)
    {
        // Merge arrays: combine unique values
        if (newer.Interests?.Any() == true)
        {
            Interests = [.. (Interests ?? [])
                .Union(newer.Interests)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)];
        }

        if (newer.Constraints?.Any() == true)
        {
            Constraints = [.. (Constraints ?? [])
                .Union(newer.Constraints)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)];
        }

        if (newer.PersonalityHints?.Any() == true)
        {
            PersonalityHints = [.. (PersonalityHints ?? [])
                .Union(newer.PersonalityHints)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)];
        }

        // Prefer newer non-null scalar values
        if (!string.IsNullOrEmpty(newer.ExperienceLevel))
            ExperienceLevel = newer.ExperienceLevel;

        if (!string.IsNullOrEmpty(newer.TargetRole))
            TargetRole = newer.TargetRole;

        // Update metadata
        UpdatedAt = DateTime.UtcNow;
        ExtractionCount++;
    }

    public static UserProfileEntity FromExtraction(string userId, UserProfileExtraction extraction)
    {
        return new UserProfileEntity
        {
            UserId = userId,
            Interests = extraction.Interests,
            ExperienceLevel = extraction.ExperienceLevel,
            TargetRole = extraction.TargetRole,
            Constraints = extraction.Constraints,
            PersonalityHints = extraction.PersonalityHints,
            ExtractionCount = 1
        };
    }

    public string ToPromptSummary()
    {
        var parts = new List<string>();

        if (Interests?.Any() == true)
            parts.Add($"Interests: {string.Join(", ", Interests)}");

        if (!string.IsNullOrEmpty(ExperienceLevel))
            parts.Add($"Experience: {ExperienceLevel}");

        if (!string.IsNullOrEmpty(TargetRole))
            parts.Add($"Target Role: {TargetRole}");

        if (Constraints?.Any() == true)
            parts.Add($"Constraints: {string.Join(", ", Constraints)}");

        if (PersonalityHints?.Any() == true)
            parts.Add($"Learning Style: {string.Join(", ", PersonalityHints)}");

        return parts.Any() ? string.Join(" | ", parts) : "No profile data yet";
    }
}

public class UserProfileExtraction
{
    [JsonProperty("interests")]
    public List<string>? Interests { get; set; }

    [JsonProperty("experience_level")]
    public string? ExperienceLevel { get; set; }

    [JsonProperty("target_role")]
    public string? TargetRole { get; set; }

    [JsonProperty("constraints")]
    public List<string>? Constraints { get; set; }

    [JsonProperty("personality_hints")]
    public List<string>? PersonalityHints { get; set; }
}
