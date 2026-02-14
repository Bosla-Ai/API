using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;
using Shared.Options;

namespace Service.Implementations;

public class JobMarketService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<JobMarketOptions> options,
    ILogger<JobMarketService> logger) : IJobMarketService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<JobMarketOptions> _options = options;
    private readonly ILogger<JobMarketService> _logger = logger;

    private static readonly ConcurrentDictionary<string, (DateTime ExpiresAt, MarketInsightDTO Data)> _cache = new();

    public async Task<MarketInsightDTO?> GetMarketInsightsAsync(string[] tags, string? region = null)
    {
        if (tags is null || tags.Length == 0)
            return null;

        var opts = _options.CurrentValue;
        var country = region ?? opts.DefaultCountry;

        var perTagResults = new List<MarketInsightDTO>();

        foreach (var tag in tags)
        {
            var searchQuery = BuildSearchQuery(tag);
            var cacheKey = $"{country}:{searchQuery}".ToLowerInvariant();

            if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogDebug("Market cache hit for '{Key}'", cacheKey);
                perTagResults.Add(cached.Data);
                continue;
            }

            try
            {
                var insight = await FetchFromAdzunaAsync(searchQuery, country, opts);
                if (insight is not null)
                {
                    EvictIfNeeded(opts.MaxCacheEntries);
                    _cache[cacheKey] = (DateTime.UtcNow.AddMinutes(opts.CacheMinutes), insight);
                    perTagResults.Add(insight);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Adzuna API call failed for tag '{Tag}'. Skipping.", tag);
            }
        }

        if (perTagResults.Count == 0)
            return null;

        return AggregateInsights(perTagResults, tags);
    }

    public CareerPulseDTO? CalculateReadiness(string[] userSkills, MarketInsightDTO marketInsight)
    {
        if (userSkills is null || userSkills.Length == 0 || marketInsight.TopRequiredSkills.Length == 0)
            return null;

        var userSkillSet = new HashSet<string>(userSkills, StringComparer.OrdinalIgnoreCase);
        var totalMarketSkills = marketInsight.TopRequiredSkills.Length;

        var matched = new List<string>();
        var gaps = new List<SkillGap>();

        foreach (var skill in marketInsight.TopRequiredSkills)
        {
            var count = marketInsight.SkillFrequency.GetValueOrDefault(skill, 0);
            var pct = marketInsight.TotalJobsAnalyzed > 0
                ? (int)Math.Round(100.0 * count / marketInsight.TotalJobsAnalyzed)
                : 0;

            if (userSkillSet.Contains(skill))
            {
                matched.Add(skill);
            }
            else
            {
                var extracted = SkillDictionary.ExtractSkills(skill);
                var category = extracted.Values.FirstOrDefault() ?? "General";

                gaps.Add(new SkillGap
                {
                    Skill = skill,
                    DemandPercent = pct,
                    Category = category
                });
            }
        }

        var score = totalMarketSkills > 0
            ? (int)Math.Round(100.0 * matched.Count / totalMarketSkills)
            : 0;

        var level = score switch
        {
            >= 80 => "Market Ready",
            >= 60 => "Strong Foundation",
            >= 40 => "Growing",
            >= 20 => "Building Blocks",
            _ => "Getting Started"
        };

        var insight = score switch
        {
            >= 80 => $"You're highly aligned with market demand! Focus on depth in {(gaps.Count > 0 ? gaps[0].Skill : "your strongest skills")} to stand out.",
            >= 60 => $"Solid skills base. Learning {(gaps.Count > 0 ? gaps[0].Skill : "a few trending tools")} could significantly boost your competitiveness.",
            >= 40 => $"You're on the right track. Bridge {Math.Min(3, gaps.Count)} key gaps to unlock the next career level.",
            >= 20 => $"Great start! Prioritize {(gaps.Count > 0 ? gaps[0].Skill : "core technologies")} — it appears in {(gaps.Count > 0 ? gaps[0].DemandPercent : 0)}% of job postings.",
            _ => "Everyone starts somewhere! Focus on one skill at a time — consistency beats intensity."
        };

        return new CareerPulseDTO
        {
            ReadinessScore = score,
            ReadinessLevel = level,
            MatchedSkills = [.. matched],
            TopGaps = [.. gaps.OrderByDescending(g => g.DemandPercent).Take(5)],
            Insight = insight,
            TargetRole = marketInsight.SearchQuery,
            JobsAnalyzed = marketInsight.TotalJobsAnalyzed
        };
    }

    // ── Private helpers ──────────────────────────────────────────

    private async Task<MarketInsightDTO?> FetchFromAdzunaAsync(string query, string country, JobMarketOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.AppId) || string.IsNullOrWhiteSpace(opts.AppKey))
        {
            _logger.LogDebug("Adzuna credentials not configured — skipping market search.");
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

        // Job search endpoint
        var searchUrl = $"{opts.BaseUrl}/jobs/{country}/search/1" +
                        $"?app_id={opts.AppId}" +
                        $"&app_key={opts.AppKey}" +
                        $"&results_per_page={opts.MaxResults}" +
                        $"&what={Uri.EscapeDataString(query)}" +
                        $"&content-type=application/json";

        _logger.LogInformation("Fetching market data from Adzuna: {Url}", searchUrl.Split("app_key")[0] + "app_key=***");

        var response = await client.GetAsync(searchUrl);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Adzuna search returned {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        var skillCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var jobTitles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalJobs = 0;
        decimal salaryMin = decimal.MaxValue, salaryMax = 0;
        var salaryValues = new List<decimal>();

        foreach (var job in results.EnumerateArray())
        {
            totalJobs++;

            var description = job.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";
            var title = job.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";

            var skills = SkillDictionary.ExtractSkills($"{title} {description}");
            foreach (var (skill, _) in skills)
            {
                skillCounts[skill] = skillCounts.GetValueOrDefault(skill, 0) + 1;
            }

            var normalizedTitle = NormalizeJobTitle(title);
            if (!string.IsNullOrEmpty(normalizedTitle))
            {
                jobTitles[normalizedTitle] = jobTitles.GetValueOrDefault(normalizedTitle, 0) + 1;
            }

            if (job.TryGetProperty("salary_min", out var sMin) && sMin.TryGetDecimal(out var minVal) && minVal > 0)
            {
                salaryValues.Add(minVal);
                if (minVal < salaryMin) salaryMin = minVal;
            }
            if (job.TryGetProperty("salary_max", out var sMax) && sMax.TryGetDecimal(out var maxVal) && maxVal > 0)
            {
                if (maxVal > salaryMax) salaryMax = maxVal;
                salaryValues.Add(maxVal);
            }
        }

        if (totalJobs == 0)
            return null;

        var topSkills = skillCounts
            .OrderByDescending(kv => kv.Value)
            .Take(15)
            .Select(kv => kv.Key)
            .ToArray();

        var topTitles = jobTitles
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => kv.Key)
            .ToArray();

        SalaryRange? salary = null;
        if (salaryValues.Count > 0)
        {
            salaryValues.Sort();
            var currency = country switch
            {
                "us" => "$",
                "gb" => "£",
                "de" or "fr" or "nl" or "es" or "it" or "at" or "be" => "€",
                "au" => "A$",
                "ca" => "C$",
                "in" => "₹",
                _ => "£"
            };

            salary = new SalaryRange
            {
                Min = salaryMin == decimal.MaxValue ? 0 : salaryMin,
                Max = salaryMax,
                Median = salaryValues[salaryValues.Count / 2],
                Currency = currency
            };
        }

        var insight = new MarketInsightDTO
        {
            TopRequiredSkills = topSkills,
            CommonJobTitles = topTitles,
            SkillFrequency = skillCounts
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            TotalJobsAnalyzed = totalJobs,
            Region = country,
            SearchQuery = query,
            Salary = salary
        };

        _logger.LogInformation(
            "Market insight ready: {TotalJobs} jobs, {SkillCount} unique skills, top: [{TopSkills}]",
            totalJobs, skillCounts.Count, string.Join(", ", topSkills.Take(5)));

        return insight;
    }

    private static string BuildSearchQuery(string tag)
    {
        var roleKeywords = new[] { "developer", "engineer", "architect", "designer", "analyst", "manager", "devops", "sre", "lead" };
        if (!roleKeywords.Any(r => tag.Contains(r, StringComparison.OrdinalIgnoreCase)))
        {
            return $"{tag} developer";
        }

        return tag;
    }

    private static MarketInsightDTO AggregateInsights(List<MarketInsightDTO> results, string[] originalTags)
    {
        var mergedSkills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mergedTitles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalJobs = 0;
        decimal salaryMin = decimal.MaxValue, salaryMax = 0;
        var salaryValues = new List<decimal>();
        string? currency = null;

        foreach (var result in results)
        {
            totalJobs += result.TotalJobsAnalyzed;

            foreach (var (skill, count) in result.SkillFrequency)
                mergedSkills[skill] = mergedSkills.GetValueOrDefault(skill, 0) + count;

            foreach (var title in result.CommonJobTitles)
                mergedTitles[title] = mergedTitles.GetValueOrDefault(title, 0) + 1;

            if (result.Salary is not null)
            {
                currency ??= result.Salary.Currency;
                if (result.Salary.Min > 0 && result.Salary.Min < salaryMin)
                    salaryMin = result.Salary.Min;
                if (result.Salary.Max > salaryMax)
                    salaryMax = result.Salary.Max;
                salaryValues.Add(result.Salary.Median);
            }
        }

        SalaryRange? salary = null;
        if (salaryValues.Count > 0)
        {
            salaryValues.Sort();
            salary = new SalaryRange
            {
                Min = salaryMin == decimal.MaxValue ? 0 : salaryMin,
                Max = salaryMax,
                Median = salaryValues[salaryValues.Count / 2],
                Currency = currency ?? "£"
            };
        }

        return new MarketInsightDTO
        {
            TopRequiredSkills = mergedSkills
                .OrderByDescending(kv => kv.Value)
                .Take(15)
                .Select(kv => kv.Key)
                .ToArray(),
            CommonJobTitles = mergedTitles
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .Select(kv => kv.Key)
                .ToArray(),
            SkillFrequency = mergedSkills
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            TotalJobsAnalyzed = totalJobs,
            Region = results[0].Region,
            SearchQuery = string.Join(", ", originalTags),
            Salary = salary
        };
    }


    private static string NormalizeJobTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "";

        // Remove common suffixes
        var idx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (idx > 0) title = title[..idx];

        idx = title.IndexOf(" (", StringComparison.Ordinal);
        if (idx > 0) title = title[..idx];

        return title.Trim();
    }

    private static void EvictIfNeeded(int maxEntries)
    {
        if (_cache.Count < maxEntries)
            return;

        var expired = _cache.Where(kv => kv.Value.ExpiresAt <= DateTime.UtcNow).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _cache.TryRemove(key, out _);
        // If still over limit, remove oldest
        while (_cache.Count >= maxEntries)
        {
            var oldest = _cache.OrderBy(kv => kv.Value.ExpiresAt).FirstOrDefault();
            if (oldest.Key is not null)
                _cache.TryRemove(oldest.Key, out _);
        }
    }
}
