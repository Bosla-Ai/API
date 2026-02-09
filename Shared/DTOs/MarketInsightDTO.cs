namespace Shared.DTOs;

/// <summary>
/// Aggregated labor-market insight extracted from real job postings.
/// Injected into the intent-detection prompt so tags are market-aligned.
/// </summary>
public class MarketInsightDTO
{
    /// <summary>
    /// Top skills/tools mentioned across analyzed job postings,
    /// sorted by frequency descending (e.g. "TypeScript", "Docker").
    /// </summary>
    public string[] TopRequiredSkills { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Common job titles found in the search results.
    /// </summary>
    public string[] CommonJobTitles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Skill → number of job postings that mention it.
    /// </summary>
    public Dictionary<string, int> SkillFrequency { get; set; } = new();

    /// <summary>
    /// Salary range discovered from the Adzuna salary endpoint.
    /// </summary>
    public SalaryRange? Salary { get; set; }

    /// <summary>
    /// Total number of job postings analyzed.
    /// </summary>
    public int TotalJobsAnalyzed { get; set; }

    /// <summary>
    /// Country/region used for the search.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// The original search query used.
    /// </summary>
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Format the insight as a concise prompt fragment for the LLM.
    /// </summary>
    public string ToPromptContext()
    {
        if (TopRequiredSkills.Length == 0)
            return string.Empty;

        var lines = new List<string>
        {
            $"CURRENT LABOR MARKET DATA (based on {TotalJobsAnalyzed} active job postings for \"{SearchQuery}\" in {Region.ToUpperInvariant()}):"
        };

        // Top skills with percentages
        var maxCount = SkillFrequency.Values.DefaultIfEmpty(1).Max();
        var skillLines = TopRequiredSkills
            .Take(15)
            .Select(s =>
            {
                var count = SkillFrequency.GetValueOrDefault(s, 0);
                var pct = TotalJobsAnalyzed > 0 ? (int)Math.Round(100.0 * count / TotalJobsAnalyzed) : 0;
                return $"  - {s} ({pct}% of postings)";
            });
        lines.Add("Top required skills:");
        lines.AddRange(skillLines);

        if (CommonJobTitles.Length > 0)
        {
            lines.Add($"Common job titles: {string.Join(", ", CommonJobTitles.Take(5))}");
        }

        if (Salary is not null && Salary.Min > 0)
        {
            lines.Add($"Salary range: {Salary.Currency}{Salary.Min:N0} – {Salary.Currency}{Salary.Max:N0}/year");
        }

        lines.Add("Use this data to identify gaps between the user's skills and market demands.");

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Salary range for a role/query.
/// </summary>
public class SalaryRange
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal Median { get; set; }
    public string Currency { get; set; } = "£";
}
