using Newtonsoft.Json;

namespace Shared.DTOs;

/// <summary>
/// User profile extracted from AI conversations, stored in Cosmos DB.
/// Contains interests, experience level, target role, and other career-related data.
/// </summary>
public class UserProfileEntity
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Partition key - the user's unique identifier
    /// </summary>
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's interests extracted from conversations (e.g., "web development", "AI", "mobile apps")
    /// </summary>
    [JsonProperty("interests")]
    public List<string>? Interests { get; set; }

    /// <summary>
    /// User's experience level: "beginner", "intermediate", "advanced"
    /// </summary>
    [JsonProperty("experienceLevel")]
    public string? ExperienceLevel { get; set; }

    /// <summary>
    /// User's target career role (e.g., "Backend Developer", "Data Scientist")
    /// </summary>
    [JsonProperty("targetRole")]
    public string? TargetRole { get; set; }

    /// <summary>
    /// User's constraints (e.g., "limited budget", "part-time learning", "prefers English")
    /// </summary>
    [JsonProperty("constraints")]
    public List<string>? Constraints { get; set; }

    /// <summary>
    /// Personality/learning style hints (e.g., "visual learner", "prefers hands-on projects")
    /// </summary>
    [JsonProperty("personalityHints")]
    public List<string>? PersonalityHints { get; set; }

    /// <summary>
    /// When this profile was first created
    /// </summary>
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this profile was last updated
    /// </summary>
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this profile has been updated from conversation extraction
    /// </summary>
    [JsonProperty("extractionCount")]
    public int ExtractionCount { get; set; } = 0;

    /// <summary>
    /// Smart merge: combines this profile with newer extracted data.
    /// Arrays are merged (union), non-null values from newer override older.
    /// </summary>
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

    /// <summary>
    /// Creates a profile entity from extracted conversation data
    /// </summary>
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

    /// <summary>
    /// Generates a summary string for use in AI prompts
    /// </summary>
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

/// <summary>
/// Data extracted from conversation by AI (transient, not stored directly)
/// </summary>
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
