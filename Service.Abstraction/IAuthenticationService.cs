using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Service.Abstraction;

public interface IAuthenticationService
{
    Task<ApplicationUser?> GetUserByEmailAsync(string email);

    Task<ApplicationUser?> GetUserByIdAsync(string id);

    Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);

    Task<IdentityResult> AssignUserToRoleAsync(ApplicationUser user, string roleName);

    Task<IList<string>> GetRolesAsync(ApplicationUser user);
}