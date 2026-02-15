using System.ComponentModel.DataAnnotations;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace Shared.DTOs.AdministrationDTOs.TrackDTOs;

public sealed class TrackCreateFullDTO
{
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "IconUrl is required")]
    public string IconUrl { get; set; } = "";

    [Required(ErrorMessage = "DomainId is required")]
    public int DomainId { get; set; }

    public string FixedTagsPayload { get; set; } = "";

    public ICollection<TrackSectionCreateFullDTO> Sections { get; set; } = [];
}
