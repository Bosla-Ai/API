using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Service.Abstraction;

namespace Service.Implementations;

public class AuthenticationService : IAuthenticationService
{

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager
        ,RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }
    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }

    public async Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password)
    {
        return await _userManager.CreateAsync(user, password);
    }

    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return _userManager.CheckPasswordAsync(user, password);        
    }

    public async Task<IdentityResult> AssignUserToRoleAsync(ApplicationUser user, string roleName)
    {
        return await _userManager.AddToRoleAsync(user, roleName);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IdentityResult> AddLoginAsync(ApplicationUser user, UserLoginInfo login)
    {
        var existing = await _userManager.FindByLoginAsync(login.LoginProvider, login.ProviderKey);
        if (existing != null && existing.Id != user.Id)
        {
            return IdentityResult.Failed(new IdentityError { Description = "External login already linked to another account." });
        }
        return await _userManager.AddLoginAsync(user, login);
    }

    public async Task<bool> IsLoginLinkedAsync(string userId, string loginProvider, string providerKey)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if(user == null) return false;
        var logins = await _userManager.GetLoginsAsync(user);
        return logins.Any(l => l.LoginProvider == loginProvider 
                               && l.ProviderKey == providerKey);
    }

    public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey)
    {
        return await _userManager.FindByLoginAsync(loginProvider, providerKey);
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user)
    {
        return await _userManager.GetLoginsAsync(user);
    }

    public async Task<IdentityResult> RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey)
    {
        return await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
    }
}