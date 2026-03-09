namespace Shared.DTOs;

public class NpmPackageInsightDTO
{
    public string PackageName { get; set; } = string.Empty;
    public long WeeklyDownloads { get; set; }
}

public class GitHubRepoInsightDTO
{
    public string Language { get; set; } = string.Empty;
    public int RepositoryCount { get; set; }
}

public class TechEcosystemDTO
{
    public NpmPackageInsightDTO[] NpmPackages { get; set; } = [];
    public GitHubRepoInsightDTO[] GitHubLanguages { get; set; } = [];

    public string ToPromptContext()
    {
        var lines = new List<string>();

        if (NpmPackages.Length > 0)
        {
            lines.Add("NPM ECOSYSTEM DATA (weekly download counts — indicates real production usage):");
            lines.AddRange(NpmPackages.Select(p =>
                $"  - {p.PackageName}: {FormatDownloads(p.WeeklyDownloads)} downloads/week"));
        }

        if (GitHubLanguages.Length > 0)
        {
            lines.Add("GITHUB ECOSYSTEM DATA (public repository counts — indicates community adoption):");
            lines.AddRange(GitHubLanguages.Select(g =>
                $"  - {g.Language}: {g.RepositoryCount:N0} repositories"));
        }

        if (lines.Count > 0)
            lines.Add("Higher numbers indicate wider industry adoption and more job opportunities.");

        return string.Join("\n", lines);
    }

    private static string FormatDownloads(long count) => count switch
    {
        >= 1_000_000_000 => $"{count / 1_000_000_000.0:F1}B",
        >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
        >= 1_000 => $"{count / 1_000.0:F1}K",
        _ => count.ToString("N0")
    };
}
