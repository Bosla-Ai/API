using Shared.DTOs;

namespace Service.Abstraction;

public interface IJobMarketService
{
    Task<MarketInsightDTO?> GetMarketInsightsAsync(string[] tags, string? region = null);
    CareerPulseDTO? CalculateReadiness(string[] userSkills, MarketInsightDTO marketInsight);
}
