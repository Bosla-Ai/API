namespace Shared.Options;

public class JobMarketOptions
{
    public const string SectionName = "JobMarket";

    /// <summary>
    /// Adzuna application ID (free registration at developer.adzuna.com).
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Adzuna application key.
    /// </summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the Adzuna API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.adzuna.com/v1/api";

    /// <summary>
    /// ISO country code for job searches (e.g. "gb", "us", "de").
    /// </summary>
    public string DefaultCountry { get; set; } = "gb";

    /// <summary>
    /// Number of job postings to fetch per search (max 50).
    /// </summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>
    /// Request timeout in seconds for Adzuna API calls.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// How long to keep cached market insights, in minutes.
    /// Uses a lightweight ConcurrentDictionary — NOT MemoryCache.
    /// </summary>
    public int CacheMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    /// Maximum number of cached entries (oldest evicted first).
    /// Each entry is ~2 KB so 50 entries ≈ 100 KB.
    /// </summary>
    public int MaxCacheEntries { get; set; } = 50;
}
