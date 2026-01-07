using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.AdministrationDTOs.DomainDTOs;

public sealed class DomainUpdateDTO
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    public string Title { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; set; }

    [Required(ErrorMessage = "Icon URL is required.")]
    [Url(ErrorMessage = "IconUrl must be a valid URL format (e.g., https://example.com/icon.png).")]
    public string IconUrl { get; set; }

    public bool IsActive { get; set; } = true;
}