namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapDetailsResponseDTO : RoadmapListResponseDTO
{
    public IEnumerable<RoadmapCourseResponseDTO> Courses { get; set; } = [];
}
