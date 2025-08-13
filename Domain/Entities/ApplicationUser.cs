using Microsoft.AspNetCore.Identity;
using Shared.Enums;

namespace Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime DateJoined { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    // Navigation Properties
    public Customer? CustomerProfile { get; set; }
    public ICollection<RoadMap> RoadMaps { get; set; } = new List<RoadMap>();
}