using System.Security.Claims;
using Domain.Entities;
using Domain.Requests;
using Domain.Responses;
using Microsoft.AspNetCore.Identity;
using Shared.DTOs.ApplicationUserDTOs;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;

namespace Service.Abstraction;

public interface IAuthenticationService
{
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
    Task<APIResponse> RegisterCustomerAsync(CustomerRegisterDTO customerDto);
    Task<(APIResponse<LoginClientResponse> apiResponse
        , LoginServerResponse loginServerResponse)> LoginAsync(LoginDTO loginDto, Guid? deviceId = null);
    Task<APIResponse> LogoutThisDeviceAsync(LogoutRequest logoutRequest, string userId);
    Task<APIResponse> LogoutAllDevicesAsync(LogoutForAllRequest logoutRequest, string userId);
    Task<(APIResponse<LoginClientResponse> apiResponse
        , LoginServerResponse loginServerResponse)> RefreshAsync(RefreshRequest refreshRequest);
    Task<APIResponse<ApplicationUserDTO>> GetMeAsync(string userId);
    Task<LoginServerResponse> GoogleLoginAsync(ClaimsPrincipal principal, string provider, string returnUrl = "/");
    Task<LoginServerResponse> GitHubLoginAsync(ClaimsPrincipal principal, string provider, string returnUrl = "/");
    Task<LoginServerResponse> LinkedInLoginAsync(ClaimsPrincipal principal, string provider, string returnUrl = "/");
    Task<ApplicationUser?> GetUserByIdAsync(string id);

    Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password);
    Task<IdentityResult> CreateUserAsync(ApplicationUser user);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);

    Task<IdentityResult> AssignUserToRoleAsync(ApplicationUser user, string roleName);

    Task<IList<string>> GetRolesAsync(ApplicationUser user);
    Task<IdentityResult> AddLoginAsync(ApplicationUser user, UserLoginInfo login);
    Task<bool> IsLoginLinkedAsync(string userId, string loginProvider, string providerKey);
    Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey);
    Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user);
    Task<IdentityResult> RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey);
}
