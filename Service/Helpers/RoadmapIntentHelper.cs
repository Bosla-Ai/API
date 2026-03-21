using Shared.DTOs.RoadmapDTOs;

namespace Service.Helpers;

public static class RoadmapIntentHelper
{
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

    public static RoadmapRequestDTO BuildRoadmapFallbackRequest(string query, string conversationContext)
    {
        var combined = string.IsNullOrWhiteSpace(conversationContext)
            ? query
            : $"{conversationContext}\n{query}";

        var extracted = ExtractKeywordsFromText(combined)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        var tags = extracted.Length > 0
            ? extracted
            :
            [
                "Programming Fundamentals",
                "Problem Solving",
                "Projects",
                "Version Control",
                "Career Preparation"
            ];

        var preferPaid = !ContainsFreeOnlyPreference(query, conversationContext);
        var language = IsArabicText(query) ? "ar" : "en";
        var sources = preferPaid
            ? new[] { "youtube", "udemy" }
            : ["youtube"];

        var checkpoints = tags.ToDictionary(
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
            Tags = tags,
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
}
