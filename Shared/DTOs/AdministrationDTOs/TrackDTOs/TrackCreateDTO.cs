using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.AdministrationDTOs.TrackDTOs;

public sealed class TrackCreateDTO
{
    [Required(ErrorMessage = "DomainId is required")]
    public int DomainId { get; set; }
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = "";
    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; } = "";
    [Required(ErrorMessage = "Icon URL is required")]
    [Url(ErrorMessage = "IconUrl must be a valid URL format (e.g., https://example.com/icon.png).")]
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Fixed Tags Payload is required")]
    public string FixedTagsPayload { get; set; } = "";
}