using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs.RoadmapDTOs;

public class RoadmapDTO
{
    [Required]
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public RoadmapSourceType SourceType { get; set; } = RoadmapSourceType.ManualSelection;

    public string? TargetJobRole { get; set; }

    [Required]
    public RoadmapGenerationDTO RoadmapData { get; set; } = null!;
}