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
    Task<IdentityResult> AddLoginAsync(ApplicationUser user, UserLoginInfo login);
    Task<bool> IsLoginLinkedAsync(string userId, string loginProvider, string providerKey);
    Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey);
    Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user);
    Task<IdentityResult> RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey);
}
