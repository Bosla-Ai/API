namespace Shared.DTOs;

public class MarketInsightDTO
{
    public string[] TopRequiredSkills { get; set; } = Array.Empty<string>();
    public string[] CommonJobTitles { get; set; } = Array.Empty<string>();
    public Dictionary<string, int> SkillFrequency { get; set; } = new();
    public SalaryRange? Salary { get; set; }
    public int TotalJobsAnalyzed { get; set; }
    public string Region { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;

    public string ToPromptContext()
    {
        if (TopRequiredSkills.Length == 0)
            return string.Empty;

        var lines = new List<string>
        {
            $"CURRENT LABOR MARKET DATA (based on {TotalJobsAnalyzed} active job postings for \"{SearchQuery}\" in {Region.ToUpperInvariant()}):"
        };

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

public class SalaryRange
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal Median { get; set; }
    public string Currency { get; set; } = "£";
}
