using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;

namespace Service.Implementations;

public class TechEcosystemService(
    TechEcosystemHelper helper,
    ILogger<TechEcosystemService> logger) : ITechEcosystemService
{
    public async Task<TechEcosystemDTO?> GetEcosystemInsightsAsync(string[] tags, CancellationToken cancellationToken = default)
    {
        if (tags.Length == 0) return null;

        var npmTask = helper.FetchNpmInsightsAsync(tags, cancellationToken);
        var githubTask = helper.FetchGitHubInsightsAsync(tags, cancellationToken);

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
}
