using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.LoginDTOs;

public sealed class LoginDTO
{
    [EmailAddress]
    public string Email { get; set; }
    [DataType(DataType.Password)]
    public string Password { get; set; }
}