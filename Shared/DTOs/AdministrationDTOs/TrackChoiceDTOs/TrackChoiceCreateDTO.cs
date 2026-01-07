using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;

public sealed class TrackChoiceCreateDTO
{
    [Required(ErrorMessage = "SectionId is required")]
    public int SectionId { get; set; }
    [Required(ErrorMessage = "SectionId is required")]
    public string Label { get; set; }
    [Required(ErrorMessage = "TagsPayload is required")]
    public string TagsPayload { get; set; }
    [Required(ErrorMessage = "IsDefault is required")]
    public bool IsDefault { get; set; } = false;
}