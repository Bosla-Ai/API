using Shared.DTOs;

namespace Service.Abstraction;

public interface IStackExchangeService
{
    Task<TechTagInsightsDTO?> GetTagInsightsAsync(string[] tags, CancellationToken cancellationToken = default);
}
