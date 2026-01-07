using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

public sealed class TrackSectionDTO
{
    public int Id { get; set; }
    [Required(ErrorMessage = "TrackId is required")]
    public int TrackId { get; set; }
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = "";
    [Required(ErrorMessage = "IsMultiSelect is required")]
    public bool IsMultiSelect { get; set; } = false;
    [Required(ErrorMessage = "OrderIndex is required")]
    public int OrderIndex { get; set; }
}