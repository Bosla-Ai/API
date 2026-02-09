namespace Shared.Options;

public class JobMarketOptions
{
    public const string SectionName = "JobMarket";

    public string AppId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.adzuna.com/v1/api";
    public string DefaultCountry { get; set; } = "gb";
    public int MaxResults { get; set; } = 20;
    public int TimeoutSeconds { get; set; } = 5;
    public int CacheMinutes { get; set; } = 1440;
    public int MaxCacheEntries { get; set; } = 50;
}
