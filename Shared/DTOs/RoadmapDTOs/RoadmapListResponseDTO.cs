using Shared.Enums;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapListResponseDTO
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoadmapSourceType SourceType { get; set; }
    public string? TargetJobRole { get; set; }
    public DateTime CreatedAt { get; set; }
}
