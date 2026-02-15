namespace Shared.DTOs.ApplicationUserDTOs;

public sealed class ApplicationUserDTO
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string Role { get; set; }
    public DateTime DateJoined { get; set; }
    public bool IsActive { get; set; } = true;
}