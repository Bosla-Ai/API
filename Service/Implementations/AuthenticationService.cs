using Domain.Entities;
using Domain.Contracts;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Identity;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs.LoginDTOs;
using Shared.Parameters;

namespace Service.Implementations;

public class AuthenticationService(
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    AuthenticationHelper accountHelper) : IAuthenticationService
{

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await userManager.FindByEmailAsync(email);
    }

    public async Task<LoginResponse> LoginAsync(LoginDTO loginDto)
    {
        if (loginDto == null)
            throw new BadRequestException("Login data is required.");

        var user = await userManager.FindByEmailAsync(loginDto.Email);
        if (user == null || !await userManager
                .CheckPasswordAsync(user, loginDto.Password))
            throw new UnauthorizedException("Invalid credentials.");

        var (loginResponse, refreshEntity) = await accountHelper
            .GenerateAndStoreTokensAsync(user, Guid.NewGuid());

        var existing =
            await refreshTokenService
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = loginResponse.DeviceId, UserId = user.Id
                });

        if (existing != null && existing.Any())
        {
            foreach (var item in existing)
            {
                item.IsRevoked = true;
                item.RevokedAt = DateTime.UtcNow;
                item.RevokedReason = "New login from same device";
                await refreshTokenService.UpdateAsync(item);
            }
        }
        await unitOfWork.SaveChangesAsync();
        return loginResponse;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string id)
    {
        return await userManager.FindByIdAsync(id);
    }

    public async Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password)
    {
        return await userManager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> CreateUserAsync(ApplicationUser user)
    {
        return await userManager.CreateAsync(user);
    }

    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return userManager.CheckPasswordAsync(user, password);        
    }

    public async Task<IdentityResult> AssignUserToRoleAsync(ApplicationUser user, string roleName)
    {
        return await userManager.AddToRoleAsync(user, roleName);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
    {
        return await userManager.GetRolesAsync(user);
    }

    public async Task<IdentityResult> AddLoginAsync(ApplicationUser user, UserLoginInfo login)
    {
        var existing = await userManager.FindByLoginAsync(login.LoginProvider, login.ProviderKey);
        if (existing != null && existing.Id != user.Id)
        {
            return IdentityResult.Failed(new IdentityError
                { Description = "External login already linked to another account." });
        }
        return await userManager.AddLoginAsync(user, login);
    }

    public async Task<bool> IsLoginLinkedAsync(string userId, string loginProvider, string providerKey)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return false;
        var logins = await userManager.GetLoginsAsync(user);
        return logins.Any(l => l.LoginProvider == loginProvider
                               && l.ProviderKey == providerKey);
    }

    public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey)
    {
        return await userManager.FindByLoginAsync(loginProvider, providerKey);
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user)
    {
        return await userManager.GetLoginsAsync(user);
    }

    public async Task<IdentityResult> RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey)
    {
        return await userManager.RemoveLoginAsync(user, loginProvider, providerKey);
    }
}