using Domain.Responses;
using Shared.DTOs.RoadmapDTOs;

namespace Service.Abstraction;

public interface IRoadmapService
{
    Task<APIResponse<RoadmapGenerationDTO>> GenerateRoadmapAsync(string[] tags, string language, bool preferPaid);
    Task<APIResponse<int>> SaveRoadmapAsync(string customerId, RoadmapDTO request);
    Task<APIResponse> DeleteRoadmapAsync(int roadmapId, string userId);
    Task<APIResponse<IEnumerable<RoadmapListResponseDTO>>> GetAllUserRoadmapsAsync(string userId);
    Task<APIResponse<RoadmapDetailsResponseDTO>> GetRoadmapDetailsAsync(int roadmapId, string userId);
    Task<APIResponse> ToggleCourseCompletionAsync(int roadmapId, int courseId, string userId);
    Task<APIResponse> UpdateCourseProgressAsync(int roadmapId, int courseId, string userId, CourseProgressUpdateDTO dto);
    Task<APIResponse<RoadmapProgressResponseDTO>> GetRoadmapProgressAsync(int roadmapId, string userId);
}
