namespace Shared.DTOs.ApplicationUserDTOs;

public sealed class ApplicationUserDTO
{
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime DateJoined { get; set; }
    public bool IsActive { get; set; } = true;
}