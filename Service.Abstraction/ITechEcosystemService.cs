using Shared.DTOs;

namespace Service.Abstraction;

public interface ITechEcosystemService
{
    Task<TechEcosystemDTO?> GetEcosystemInsightsAsync(string[] tags, CancellationToken cancellationToken = default);
}
