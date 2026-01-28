using System.ComponentModel.DataAnnotations;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace Shared.DTOs.AdministrationDTOs.TrackDTOs;

public sealed class TrackUpdateFullDTO
{
    [Required]
    public int Id { get; set; }

    public int? DomainId { get; set; }

    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Icon URL is required")]
    [Url(ErrorMessage = "IconUrl must be a valid URL format.")]
    public string IconUrl { get; set; } = "";

    public bool IsActive { get; set; } = true;

    [Required(AllowEmptyStrings = true)]
    public string FixedTagsPayload { get; set; } = "";

    public ICollection<TrackSectionUpdateFullDTO>? Sections { get; set; }
}
