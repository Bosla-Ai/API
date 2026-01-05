using Shared.DTOs.RoadmapDTOs;

namespace Service.Abstraction;

public interface IRoadmapService 
{
    Task<RoadmapGenerationDTO> GenerateRoadmapAsync(string[] tags, string level, string language, bool preferPaid);
    Task<bool> SaveRoadmapAsync(string customerId, RoadmapDTO request);
}