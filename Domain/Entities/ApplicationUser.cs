using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

public sealed class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime DateJoined { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    // Navigation Properties
    public Customer? CustomerProfile { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<ApplicationUserRole> UserRoles { get; set; } = [];
}
