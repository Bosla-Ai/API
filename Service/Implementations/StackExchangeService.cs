using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;
using Shared.Options;

namespace Service.Implementations;

public class StackExchangeService(
    StackExchangeHelper helper,
    IOptionsMonitor<StackExchangeOptions> options,
    ILogger<StackExchangeService> logger) : IStackExchangeService
{
    public async Task<TechTagInsightsDTO?> GetTagInsightsAsync(string[] tags, CancellationToken cancellationToken = default)
    {
        var config = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(config.ApiKey) || tags.Length == 0)
            return null;

        var results = new List<TechTagInsightDTO>();

        var normalizedTags = tags
            .Select(StackExchangeHelper.NormalizeTag)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(10)
            .ToArray();

        if (normalizedTags.Length == 0)
            return null;

        try
        {
            var tagsBatch = string.Join(";", normalizedTags);
            var tagInfos = await helper.FetchTagInfoAsync(tagsBatch, config, cancellationToken);

            if (tagInfos is not null)
                results.AddRange(tagInfos);

            var relatedTasks = normalizedTags.Take(3)
                .Select(async tag =>
                {
                    var related = await helper.FetchRelatedTagsAsync(tag, config, cancellationToken);
                    return (tag, related);
                })
                .ToArray();

            var relatedResults = await Task.WhenAll(relatedTasks);
            foreach (var (tag, related) in relatedResults)
            {
                var existing = results.FirstOrDefault(r =>
                    string.Equals(r.TagName, tag, StringComparison.OrdinalIgnoreCase));
                if (existing is not null && related.Length > 0)
                {
                    existing.RelatedTags = related;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StackExchange API error for tags: {Tags}", string.Join(", ", normalizedTags));
            return null;
        }

        return results.Count > 0 ? new TechTagInsightsDTO { Tags = results.ToArray() } : null;
    }
}
