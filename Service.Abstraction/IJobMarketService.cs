using Shared.DTOs;

namespace Service.Abstraction;

/// <summary>
/// Fetches real-time labor-market insights (skills demand, salary, job titles)
/// from external job APIs (Adzuna) to align roadmap tags with actual market needs.
/// </summary>
public interface IJobMarketService
{
    /// <summary>
    /// Analyse current job postings for the given tags/skills and return
    /// aggregated market intelligence.
    /// </summary>
    /// <param name="tags">User's interests / skill tags (e.g. ["React", "Node.js"]).</param>
    /// <param name="region">ISO country code override (null → use default).</param>
    /// <returns>Market insight or null when the external API is unreachable.</returns>
    Task<MarketInsightDTO?> GetMarketInsightsAsync(string[] tags, string? region = null);
}
