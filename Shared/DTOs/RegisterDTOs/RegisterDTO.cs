using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs.RegisterDTOs;

public sealed class RegisterDTO
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    [EmailAddress]
    public string Email { get; set; }
    [DataType(DataType.Password)]
    public string Password { get; set; }
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string PasswordConfirm { get; set; }
    [Phone]
    public string PhoneNumber { get; set; }
}