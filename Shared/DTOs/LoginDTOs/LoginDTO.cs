using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.LoginDTOs;

public sealed class LoginDTO
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [MaxLength(128)]
    public string Password { get; set; }
}