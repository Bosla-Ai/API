using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;
using Shared.Options;

namespace Service.Implementations;

public class StackExchangeService(
    HttpClient httpClient,
    IOptionsMonitor<StackExchangeOptions> options,
    ILogger<StackExchangeService> logger) : IStackExchangeService
{
    public async Task<TechTagInsightsDTO?> GetTagInsightsAsync(string[] tags, CancellationToken cancellationToken = default)
    {
        var config = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(config.ApiKey) || tags.Length == 0)
            return null;

        var results = new List<TechTagInsightDTO>();

        // Batch tags into semicolon-separated groups (StackExchange supports up to 20 per call)
        var normalizedTags = tags
            .Select(t => NormalizeTag(t))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(10)
            .ToArray();

        if (normalizedTags.Length == 0)
            return null;

        try
        {
            var tagsBatch = string.Join(";", normalizedTags);
            var tagInfos = await FetchTagInfoAsync(tagsBatch, config, cancellationToken);

            if (tagInfos is not null)
                results.AddRange(tagInfos);

            // Fetch related tags for the top 3 tags in parallel
            var relatedTasks = normalizedTags.Take(3)
                .Select(async tag =>
                {
                    var related = await FetchRelatedTagsAsync(tag, config, cancellationToken);
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

    private async Task<TechTagInsightDTO[]?> FetchTagInfoAsync(string tagsBatch, StackExchangeOptions config, CancellationToken cancellationToken)
    {
        var encodedTags = Uri.EscapeDataString(tagsBatch);
        var url = $"{config.BaseUrl}/tags/{encodedTags}/info?site=stackoverflow&key={config.ApiKey}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var json = await FetchGzippedJsonAsync(url, cts.Token);
        if (json is null) return null;

        var results = new List<TechTagInsightDTO>();
        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                var count = item.GetProperty("count").GetInt32();
                var hasSynonyms = item.TryGetProperty("has_synonyms", out var syn) && syn.GetBoolean();

                results.Add(new TechTagInsightDTO
                {
                    TagName = name,
                    QuestionCount = count,
                    HasSynonyms = hasSynonyms
                });
            }
        }

        return results.ToArray();
    }

    private async Task<string[]> FetchRelatedTagsAsync(string tag, StackExchangeOptions config, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(tag);
        var url = $"{config.BaseUrl}/tags/{encodedTag}/related?site=stackoverflow&key={config.ApiKey}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var json = await FetchGzippedJsonAsync(url, cts.Token);
        if (json is null) return [];

        var related = new List<string>();
        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray().Take(5))
            {
                var name = item.GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    related.Add(name);
            }
        }

        return related.ToArray();
    }

    private async Task<JsonDocument?> FetchGzippedJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        request.Headers.UserAgent.ParseAdd("Bosla/1.0");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("StackExchange API returned {StatusCode}: {Error}", response.StatusCode, error);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        // StackExchange always returns gzip-compressed responses
        Stream decompressed = response.Content.Headers.ContentEncoding.Contains("gzip")
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;

        await using (decompressed)
        {
            return await JsonDocument.ParseAsync(decompressed, cancellationToken: cancellationToken);
        }
    }

    private static string NormalizeTag(string tag)
    {
        // Convert "ASP.NET Core Web API" → "asp.net-core" / "C# Fundamentals" → "c#"
        // StackOverflow tags are lowercase with hyphens
        var normalized = TagNormalizer.StripSuffixes(tag);

        // Replace spaces with hyphens (SO tag format)
        normalized = normalized.Replace(' ', '-');

        // Known mappings for common tech terms
        return normalized switch
        {
            "c#" or "csharp" => "c#",
            "c++" or "cpp" => "c++",
            "javascript" or "js" => "javascript",
            "typescript" or "ts" => "typescript",
            "asp.net-core" or "aspnet-core" or "asp.net-core-web-api" => "asp.net-core",
            "entity-framework-core" or "ef-core" => "entity-framework-core",
            "react" or "reactjs" or "react.js" => "reactjs",
            "angular" or "angularjs" => "angular",
            "vue" or "vuejs" or "vue.js" => "vue.js",
            "node" or "nodejs" or "node.js" => "node.js",
            "python" => "python",
            "java" => "java",
            "docker" => "docker",
            "kubernetes" or "k8s" => "kubernetes",
            "aws" => "amazon-web-services",
            "azure" => "azure",
            "sql" => "sql",
            "postgresql" or "postgres" => "postgresql",
            "mongodb" or "mongo" => "mongodb",
            "redis" => "redis",
            "git" => "git",
            "linux" => "linux",
            "django" => "django",
            "flask" => "flask",
            "spring" or "spring-boot" => "spring-boot",
            "machine-learning" or "ml" => "machine-learning",
            "deep-learning" => "deep-learning",
            "data-science" => "data-science",
            _ => normalized
        };
    }
}
