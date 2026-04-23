using System.Text;
using System.Text.Json;
using Shared.DTOs;
using Shared.DTOs.RoadmapDTOs;

namespace Service.Helpers;

public static class RoadmapIntentHelper
{
    private static readonly Dictionary<string, string> RoadmapTokenAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "engineer",
        ["engr"] = "engineer",
        ["dev"] = "developer"
    };

    public const string RoadmapStatePrefix = "roadmap_state:";
    public const string RoadmapStatePendingConfirmation = "pending_confirmation";
    public const string RoadmapStateDiscoveryAsked = "discovery_asked";
    public const string RoadmapStateCompleted = "completed";
    public const string RoadmapStateIdle = "idle";
    public const string RoadmapRequestPrefix = "roadmap_request:";

    public static string[] ExtractKeywordsFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 10)
            return [];

        var words = text.Split([' ', ',', '.', '!', '?', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var keywords = new List<string>();

        foreach (var word in words)
        {
            var clean = word.Trim('\'', '"', '(', ')', '[', ']');
            if (clean.Length < 2) continue;

            var found = SkillDictionary.ExtractSkills(clean);
            if (found.Count > 0)
            {
                keywords.AddRange(found.Keys);
            }
        }

        var fullText = string.Join(" ", words);
        var multiWordSkills = SkillDictionary.ExtractSkills(fullText);
        foreach (var (skill, _) in multiWordSkills)
        {
            keywords.Add(skill);
        }

        return [.. keywords.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public static bool HasRoadmapContext(string conversationContext)
    {
        if (string.IsNullOrWhiteSpace(conversationContext))
            return false;

        var normalized = conversationContext.ToLowerInvariant();
        return normalized.Contains("roadmap")
               || normalized.Contains("learning path")
               || normalized.Contains("generate your roadmap")
               || normalized.Contains("خريطة طريق")
               || normalized.Contains("مسار تعلم")
               || normalized.Contains("خطة تعلم")
               || normalized.Contains("رودماب");
    }

    public static RoadmapRequestDTO BuildRoadmapFallbackRequest(
        string query,
        string conversationContext,
        UserProfileEntity? userProfile = null,
        string? detectedLanguage = null)
    {
        var tags = new List<string>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddTag(string? value)
        {
            var normalized = NormalizeRoadmapTag(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (seenTags.Add(normalized))
                tags.Add(normalized);
        }

        AddTag(userProfile?.TargetRole);

        if (userProfile?.Interests is { Count: > 0 })
        {
            foreach (var interest in userProfile.Interests)
                AddTag(interest);
        }

        var extractionSources = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
            extractionSources.Add(query);
        if (!string.IsNullOrWhiteSpace(conversationContext))
            extractionSources.Add(conversationContext);
        if (!string.IsNullOrWhiteSpace(userProfile?.TargetRole))
            extractionSources.Add(userProfile.TargetRole);

        foreach (var source in extractionSources)
        {
            foreach (var extractedTag in ExtractKeywordsFromText(source))
                AddTag(extractedTag);
        }

        if (tags.Count == 0)
        {
            tags.AddRange(
            [
                "Programming Fundamentals",
                "Problem Solving",
                "Projects",
                "Version Control",
                "Career Preparation"
            ]);
        }

        var finalTags = tags.Take(8).ToArray();

        var userWantsFreeOnly = ContainsFreeOnlyPreference(query, conversationContext)
            || (userProfile?.Constraints?.Any(c =>
                c.Contains("free only", StringComparison.OrdinalIgnoreCase)
                || c.Contains("no paid", StringComparison.OrdinalIgnoreCase)
                || c.Contains("مجاني", StringComparison.OrdinalIgnoreCase)
                || c.Contains("بدون مدفوع", StringComparison.OrdinalIgnoreCase)) ?? false);

        var preferPaid = !userWantsFreeOnly;
        var language = !string.IsNullOrWhiteSpace(detectedLanguage)
            ? detectedLanguage
            : IsArabicText(query) ? "ar" : "en";
        var sources = preferPaid
            ? new[] { "youtube", "udemy" }
            : ["youtube"];

        var checkpoints = finalTags.ToDictionary(
            tag => tag,
            tag => language == "ar"
                ? new[]
                {
                    $"أكمل أساسيات {tag}",
                    $"طبّق مشروعًا عمليًا باستخدام {tag}"
                }
                :
                [
                    $"Complete fundamentals of {tag}",
                    $"Build one practical project using {tag}"
                ],
            StringComparer.OrdinalIgnoreCase
        );

        return new RoadmapRequestDTO
        {
            Tags = finalTags,
            PreferPaid = preferPaid,
            Language = language,
            Sources = sources,
            TagCheckpoints = checkpoints
        };
    }

    private static bool ContainsFreeOnlyPreference(string query, string conversationContext)
    {
        var text = $"{query}\n{conversationContext}".ToLowerInvariant();
        return text.Contains("free")
               || text.Contains("only free")
               || text.Contains("no paid")
               || text.Contains("مجاني")
               || text.Contains("مجانا")
               || text.Contains("مش مدفوع")
               || text.Contains("بدون مدفوع")
               || text.Contains("مش عايز مدفوع")
               || text.Contains("مافيش مدفوع");
    }

    private static bool IsArabicText(string text)
    {
        foreach (var ch in text)
        {
            if (ch >= '\u0600' && ch <= '\u06FF')
                return true;
        }

        return false;
    }

    public static string BuildRoadmapStateMessage(string state)
    {
        return $"{RoadmapStatePrefix}{state}";
    }

    public static string BuildRoadmapRequestMessage(RoadmapRequestDTO? request)
    {
        if (request is null)
            return $"{RoadmapRequestPrefix}null";

        var json = JsonSerializer.Serialize(request);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return $"{RoadmapRequestPrefix}{encoded}";
    }

    public static string? ExtractRoadmapState(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        if (!message.StartsWith(RoadmapStatePrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return message[RoadmapStatePrefix.Length..].Trim().ToLowerInvariant();
    }

    public static RoadmapRequestDTO? ExtractRoadmapRequest(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        if (!message.StartsWith(RoadmapRequestPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var payload = message[RoadmapRequestPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(payload) || string.Equals(payload, "null", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            return JsonSerializer.Deserialize<RoadmapRequestDTO>(json);
        }
        catch
        {
            try
            {
                return JsonSerializer.Deserialize<RoadmapRequestDTO>(payload);
            }
            catch
            {
                return null;
            }
        }
    }

    public static string NormalizeRoadmapTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var tokens = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => RoadmapTokenAliases.TryGetValue(token, out var expanded)
                ? expanded
                : token)
            .ToArray();

        return string.Join(" ", tokens).Trim();
    }
}
