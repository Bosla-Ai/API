using System.Text.Json;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;

namespace Service.Implementations;

public class TechEcosystemService(
    HttpClient httpClient,
    ILogger<TechEcosystemService> logger) : ITechEcosystemService
{
    private const string NpmApiBaseUrl = "https://api.npmjs.org/downloads/point/last-week";
    private const string GitHubApiBaseUrl = "https://api.github.com/search/repositories";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task<TechEcosystemDTO?> GetEcosystemInsightsAsync(string[] tags, CancellationToken cancellationToken = default)
    {
        if (tags.Length == 0) return null;

        var npmTask = FetchNpmInsightsAsync(tags, cancellationToken);
        var githubTask = FetchGitHubInsightsAsync(tags, cancellationToken);

        await Task.WhenAll(npmTask, githubTask);

        var npmResults = await npmTask;
        var githubResults = await githubTask;

        if (npmResults.Length == 0 && githubResults.Length == 0)
            return null;

        return new TechEcosystemDTO
        {
            NpmPackages = npmResults,
            GitHubLanguages = githubResults
        };
    }

    private async Task<NpmPackageInsightDTO[]> FetchNpmInsightsAsync(string[] tags, CancellationToken cancellationToken)
    {
        // Map tags to npm packages, deduplicate
        var packages = tags
            .Select(NpmPackageMapper.TryMapToPackage)
            .Where(p => p is not null)
            .Distinct()
            .Take(5)
            .ToArray();

        var tasks = packages.Select(async package =>
        {
            try
            {
                var encodedPackage = Uri.EscapeDataString(package!);
                var url = $"{NpmApiBaseUrl}/{encodedPackage}";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(RequestTimeout);
                using var response = await httpClient.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode) return null;

                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

                if (json.RootElement.TryGetProperty("downloads", out var downloads))
                {
                    return new NpmPackageInsightDTO
                    {
                        PackageName = package!,
                        WeeklyDownloads = downloads.GetInt64()
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to fetch npm downloads for {Package}", package);
            }
            return null;
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).OrderByDescending(r => r!.WeeklyDownloads).ToArray()!;
    }

    private async Task<GitHubRepoInsightDTO[]> FetchGitHubInsightsAsync(string[] tags, CancellationToken cancellationToken)
    {
        // Only fetch top 3 to stay within GitHub's unauthenticated rate limit (10 req/min)
        var languages = tags
            .Select(NormalizeToGitHubLanguage)
            .Where(l => l is not null)
            .Distinct()
            .Take(3)
            .ToArray();

        var tasks = languages.Select(async language =>
        {
            try
            {
                var encodedQuery = Uri.EscapeDataString($"language:{language} stars:>100");
                var url = $"{GitHubApiBaseUrl}?q={encodedQuery}&per_page=1";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(RequestTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/vnd.github.v3+json");
                request.Headers.Add("User-Agent", "BoslaAPI");

                using var response = await httpClient.SendAsync(request, cts.Token);

                if (!response.IsSuccessStatusCode) return null;

                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

                if (json.RootElement.TryGetProperty("total_count", out var totalCount))
                {
                    return new GitHubRepoInsightDTO
                    {
                        Language = language!,
                        RepositoryCount = totalCount.GetInt32()
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to fetch GitHub repos for {Language}", language);
            }
            return null;
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).OrderByDescending(r => r!.RepositoryCount).ToArray()!;
    }

    private static string? NormalizeToGitHubLanguage(string tag)
    {
        var normalized = TagNormalizer.StripSuffixes(tag);

        return normalized switch
        {
            "c#" or "csharp" or "asp.net core" or "entity framework core" or "linq" => "C#",
            "javascript" or "js" or "react" or "reactjs" or "angular" or "vue" or "vuejs" or "node" or "nodejs" or "node.js" or "express" or "next.js" or "nextjs" => "JavaScript",
            "typescript" or "ts" => "TypeScript",
            "python" or "django" or "flask" or "fastapi" => "Python",
            "java" or "spring" or "spring-boot" => "Java",
            "go" or "golang" => "Go",
            "rust" => "Rust",
            "c++" or "cpp" => "C++",
            "ruby" or "rails" or "ruby-on-rails" => "Ruby",
            "php" or "laravel" => "PHP",
            "swift" => "Swift",
            "kotlin" => "Kotlin",
            "dart" or "flutter" => "Dart",
            "r" => "R",
            "scala" => "Scala",
            "shell" or "bash" => "Shell",
            _ => null
        };
    }
}
