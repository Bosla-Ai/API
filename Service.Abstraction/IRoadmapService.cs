using Domain.Responses;
using Shared.DTOs.RoadmapDTOs;

namespace Service.Abstraction;

public interface IRoadmapService
{
    Task<APIResponse<RoadmapGenerationDTO>> GenerateRoadmapAsync(string[] tags, string language, bool preferPaid);
    Task<APIResponse<int>> SaveRoadmapAsync(string customerId, RoadmapDTO request);
    Task<APIResponse> DeleteRoadmapAsync(int roadmapId, string userId);
}
