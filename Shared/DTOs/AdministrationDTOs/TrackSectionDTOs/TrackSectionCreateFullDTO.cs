using System.ComponentModel.DataAnnotations;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;

namespace Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

public sealed class TrackSectionCreateFullDTO
{
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "IsMultiSelect is required")]
    public bool IsMultiSelect { get; set; } = false;

    [Required(ErrorMessage = "OrderIndex is required")]
    public int OrderIndex { get; set; }

    public ICollection<TrackChoiceCreateDTO> Choices { get; set; } = [];
}
