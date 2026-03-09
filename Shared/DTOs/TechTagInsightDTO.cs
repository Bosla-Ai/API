namespace Shared.DTOs;

public class TechTagInsightDTO
{
    public string TagName { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public bool HasSynonyms { get; set; }
    public string[] RelatedTags { get; set; } = [];

    public string ToPromptLine()
    {
        var related = RelatedTags.Length > 0 ? $" (related: {string.Join(", ", RelatedTags.Take(5))})" : "";
        return $"  - {TagName}: {QuestionCount:N0} questions on StackOverflow{related}";
    }
}

public class TechTagInsightsDTO
{
    public TechTagInsightDTO[] Tags { get; set; } = [];

    public string ToPromptContext()
    {
        if (Tags.Length == 0)
            return string.Empty;

        var lines = new List<string>
        {
            "TECHNOLOGY POPULARITY DATA (StackOverflow community activity):"
        };
        lines.AddRange(Tags.Select(t => t.ToPromptLine()));
        lines.Add("Technologies with more questions indicate higher community adoption and employer demand.");

        return string.Join("\n", lines);
    }
}
