using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Shared.Enums;

namespace Shared.DTOs.RegisterDTOs;

public sealed class CustomerRegisterDTO
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [MaxLength(128)]
    public string Password { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [MaxLength(128)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string PasswordConfirm { get; set; }

    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; }
}