using System.Net;
using System.Security.Claims;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Requests;
using Domain.Responses;
using Microsoft.AspNetCore.Identity;
using Service.Abstraction;
using Service.Helpers;
using Shared;
using Shared.DTOs.ApplicationUserDTOs;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;
using Shared.Parameters;

namespace Service.Implementations;

public class AuthenticationService(
    IRefreshTokenService refreshTokenService,
    ICustomerService customerService,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    AuthenticationHelper accountHelper) : IAuthenticationService
{
    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        return await userManager.FindByEmailAsync(email);
    }

    #region Linked In Auth

    public async Task<LoginServerResponse> LinkedInLoginAsync(ClaimsPrincipal principal, string provider,
        string returnUrl = "/")
    {
        // Same pattern used in Google/GitHub: require externalId + email
        var externalId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal?.FindFirst(ClaimTypes.Email)?.Value;
        var name = principal?.FindFirst(ClaimTypes.Name)?.Value;
        var firstName = principal?.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = principal?.FindFirst(ClaimTypes.Surname)?.Value;
        var pictureUrl = principal?.FindFirst("picture")?.Value;

        if (string.IsNullOrWhiteSpace(externalId))
            throw new BadRequestException("LinkedIn did not return an identifier.");
        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException("Email not received from LinkedIn provider.");

        // If GivenName/Surname not available, split the full Name claim
        if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : lastName;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName ?? (string.IsNullOrWhiteSpace(name) ? "LinkedIn User" : name),
                LastName = lastName,
                ProfilePictureUrl = pictureUrl,
                EmailConfirmed = true
            };

            var createRes = await CreateUserAsync(user);
            if (!createRes.Succeeded)
                throw new BadRequestException(createRes.Errors.Select(e => e.Description).First());

            await AssignUserToRoleAsync(user, StaticData.CustomerRoleName);

            var customer = new Customer { ApplicationUserId = user.Id };
            await customerService.CreateAsync(customer);
        }
        else
        {
            // Prevent account takeover: only allow linking if this provider is already linked
            var alreadyLinked = await IsLoginLinkedAsync(user.Id, provider, externalId);
            if (!alreadyLinked)
            {
                throw new BadRequestException(
                    "An account with this email already exists. Please sign in with your password first, then link your LinkedIn account.");
            }

            // Update profile info from provider if missing
            var updated = false;
            if (string.IsNullOrWhiteSpace(user.FirstName) || user.FirstName == "Unknown" || user.FirstName == "LinkedIn User")
            {
                user.FirstName = firstName ?? name ?? user.FirstName;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(lastName))
            {
                user.LastName = lastName;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.ProfilePictureUrl) && !string.IsNullOrWhiteSpace(pictureUrl))
            {
                user.ProfilePictureUrl = pictureUrl;
                updated = true;
            }
            if (updated)
                await userManager.UpdateAsync(user);
        }

        var alreadyLinkedFinal = await IsLoginLinkedAsync(user.Id, provider, externalId);
        if (!alreadyLinkedFinal)
        {
            var addLoginResult = await AddLoginAsync(user, new UserLoginInfo(provider, externalId, provider));
            if (!addLoginResult.Succeeded)
                throw new BadRequestException(addLoginResult.Errors.First().Description);
        }

        var loginResponse = await accountHelper.GenerateAndStoreTokensAsync(user, Guid.NewGuid());
        await unitOfWork.SaveChangesAsync();
        return loginResponse;
    }

    #endregion

    #region GithubAuthentication

    public async Task<LoginServerResponse> GitHubLoginAsync(ClaimsPrincipal principal, string provider,
        string returnUrl = "/")
    {
        var externalId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal?.FindFirst(ClaimTypes.Email)?.Value;
        var name = principal?.FindFirst(ClaimTypes.Name)?.Value ??
                   principal?.FindFirst("urn:github:login")?.Value;
        var pictureUrl = principal?.FindFirst("urn:github:avatar")?.Value ?? principal?.Claims.FirstOrDefault(c => c.Type.Contains("avatar", StringComparison.OrdinalIgnoreCase) || c.Type.Contains("picture", StringComparison.OrdinalIgnoreCase) || c.Type.Contains("image", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(externalId))
            throw new BadRequestException("GitHub did not return an identifier.");

        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException("Email not received from GitHub provider.");

        // Split the full name into first/last
        string? firstName = null;
        string? lastName = null;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : null;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName ?? "GitHub User",
                LastName = lastName,
                ProfilePictureUrl = pictureUrl,
                EmailConfirmed = true
            };

            var createRes = await CreateUserAsync(user);

            if (!createRes.Succeeded)
                throw new BadRequestException(createRes.Errors.Select(e => e.Description).FirstOrDefault()!);

            await AssignUserToRoleAsync(user, StaticData.CustomerRoleName);

            var customer = new Customer()
            {
                ApplicationUserId = user.Id,
            };
            await customerService.CreateAsync(customer);
        }
        else
        {
            // Prevent account takeover: only allow linking if this provider is already linked
            var alreadyLinked = await IsLoginLinkedAsync(user.Id, provider, externalId);
            if (!alreadyLinked)
            {
                // Require the external login to be linked from an authenticated session,
                // not automatically on first OAuth sign-in with a matching email
                throw new BadRequestException(
                    "An account with this email already exists. Please sign in with your password first, then link your GitHub account.");
            }

            // Update profile info from provider if missing
            var updated = false;
            if (string.IsNullOrWhiteSpace(user.FirstName) || user.FirstName == "Unknown" || user.FirstName == "GitHub User")
            {
                user.FirstName = firstName ?? user.FirstName;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(lastName))
            {
                user.LastName = lastName;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.ProfilePictureUrl) && !string.IsNullOrWhiteSpace(pictureUrl))
            {
                user.ProfilePictureUrl = pictureUrl;
                updated = true;
            }
            if (updated)
                await userManager.UpdateAsync(user);
        }

        var loginInfo = new UserLoginInfo(provider, externalId, provider);

        var alreadyLinkedFinal = await
            IsLoginLinkedAsync(user.Id, provider, externalId);

        if (!alreadyLinkedFinal)
        {
            var addLoginResult = await AddLoginAsync(user, loginInfo);
            if (!addLoginResult.Succeeded)
                throw new BadRequestException(addLoginResult.Errors.First().Description);
        }

        var loginResponse = await accountHelper
            .GenerateAndStoreTokensAsync(user, Guid.NewGuid());

        await unitOfWork.SaveChangesAsync();
        return loginResponse;
    }

    #endregion

    public async Task<APIResponse> RegisterCustomerAsync(CustomerRegisterDTO customerDTO)
    {
        if (customerDTO == null)
            throw new BadRequestException("Customer DTO is null");

        var userExists = await GetUserByEmailAsync(customerDTO.Email);
        if (userExists != null)
            throw new BadRequestException("Customer already exists");

        var customerUser = mapper.Map<ApplicationUser>(customerDTO);
        var customerUserCreationResult = await
            CreateUserAsync(customerUser, customerDTO.Password);

        if (!customerUserCreationResult.Succeeded)
            throw new BadRequestException(accountHelper.AddIdentityErrors(customerUserCreationResult)
                .FirstOrDefault()!);

        var customerRoleAssigningResult =
            await AssignUserToRoleAsync(customerUser, StaticData.CustomerRoleName);

        if (!customerRoleAssigningResult.Succeeded)
            throw new BadRequestException(
                accountHelper.AddIdentityErrors(customerRoleAssigningResult).FirstOrDefault()!);

        var customer = mapper.Map<Customer>(customerDTO);
        customer.ApplicationUserId = customerUser.Id;
        await customerService.CreateAsync(customer);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse<string>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = $"User {customerUser.UserName} Registered Successfully"
        };
    }

    public async Task<(APIResponse<LoginClientResponse> apiResponse, LoginServerResponse loginServerResponse)>
        LoginAsync(LoginDTO loginDto)
    {
        if (loginDto == null)
            throw new BadRequestException("Login data is required.");

        var user = await userManager.FindByEmailAsync(loginDto.Email);
        if (user == null || !await userManager
                .CheckPasswordAsync(user, loginDto.Password))
            throw new UnauthorizedException("Invalid credentials.");

        var loginServerResponse = await accountHelper
            .GenerateAndStoreTokensAsync(user, Guid.NewGuid());

        var existing =
            await refreshTokenService
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = loginServerResponse.DeviceId,
                    UserId = user.Id
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
        return (new APIResponse<LoginClientResponse>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<LoginClientResponse>(loginServerResponse)
        }, loginServerResponse);
    }

    public async Task<APIResponse> LogoutThisDeviceAsync(LogoutRequest logoutRequest)
    {
        var token = await refreshTokenService
            .GetWithDeviceIdNotRevokedAsync(new RefreshTokenParameters()
            {
                DeviceId = logoutRequest.DeviceId
            }) ?? throw new UnauthorizedException("Invalid Logout request.");
        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedReason = "User logged out";
        await refreshTokenService.UpdateAsync(token);
        await unitOfWork.SaveChangesAsync();

        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> LogoutAllDevicesAsync(LogoutForAllRequest logoutRequest)
    {
        var token = await refreshTokenService
            .GetWithDeviceIdNotRevokedAsync(new RefreshTokenParameters()
            {
                DeviceId = logoutRequest.DeviceId
            }) ?? throw new UnauthorizedException("No Active Tokens Founded");
        var tokens = await refreshTokenService
            .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
            {
                UserId = token.UserId
            });

        if (tokens == null || !tokens.Any())
            throw new BadRequestException("Invalid Logout request.");

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedReason = "User logged out for all active devices";
            await refreshTokenService.UpdateAsync(t);
        }

        await unitOfWork.SaveChangesAsync();


        return new APIResponse()
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<(APIResponse<LoginClientResponse> apiResponse
        , LoginServerResponse loginServerResponse)> RefreshAsync(RefreshRequest refreshRequest)
    {
        if (refreshRequest.DeviceId == null || string.IsNullOrWhiteSpace(refreshRequest.RefreshToken))
            throw new UnauthorizedException("Refresh or Device Id is Invalid");

        var storedTokens = await refreshTokenService
            .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
            {
                DeviceId = refreshRequest.DeviceId
            });
        if (storedTokens == null || !storedTokens.Any())
            throw new UnauthorizedException("Invalid Refresh Token");

        RefreshToken validToken = null;
        foreach (var token in storedTokens)
        {
            if (accountHelper.VerifyTokenWithSalt(
                    refreshRequest.RefreshToken
                    , token.TokenHash
                    , token.TokenSalt))
            {
                validToken = token;
                break;
            }
        }

        if (validToken == null)
        {
            var firstToken = storedTokens.First();
            var allUserTokens = await refreshTokenService
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    UserId = firstToken.UserId
                });
            foreach (var t in allUserTokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedReason = "Refresh token reuse detected";
                await refreshTokenService.UpdateAsync(t);
            }

            throw new UnauthorizedException("Refresh token reuse detected");
        }

        if (validToken.IsRevoked || validToken.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Invalid Refresh Token");

        // Revoke the consumed token (rotation: old token can never be reused)
        validToken.IsRevoked = true;
        validToken.RevokedAt = DateTime.UtcNow;
        validToken.RevokedReason = "Consumed during refresh";
        await refreshTokenService.UpdateAsync(validToken);

        var user = await GetUserByIdAsync(validToken.UserId) ?? throw new NotFoundException("User not found");
        var loginServerResponse = await accountHelper
            .GenerateAndStoreTokensAsync(user, Guid.NewGuid());

        await unitOfWork.SaveChangesAsync();

        return (new APIResponse<LoginClientResponse>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = mapper.Map<LoginClientResponse>(loginServerResponse)
        }, loginServerResponse);
    }

    public async Task<APIResponse<ApplicationUserDTO>> GetMeAsync(string userId)
    {
        var user = await GetUserByIdAsync(userId) ?? throw new NotFoundException("User not found");
        var roles = await userManager.GetRolesAsync(user);
        var userDto = mapper.Map<ApplicationUserDTO>(user);
        userDto.Role = roles.FirstOrDefault() ?? "";

        return new APIResponse<ApplicationUserDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = userDto
        };
    }


    public async Task<LoginServerResponse> GoogleLoginAsync(ClaimsPrincipal principal, string provider,
        string returnUrl = "/")
    {
        var externalId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal?.FindFirst(ClaimTypes.Email)?.Value;
        var firstName = principal?.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = principal?.FindFirst(ClaimTypes.Surname)?.Value;
        var name = principal?.FindFirst(ClaimTypes.Name)?.Value;
        var pictureUrl = principal?.FindFirst("picture")?.Value ?? principal?.Claims.FirstOrDefault(c => c.Type.Contains("picture", StringComparison.OrdinalIgnoreCase) || c.Type.Contains("image", StringComparison.OrdinalIgnoreCase) || c.Type.Contains("avatar", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(externalId))
            throw new BadRequestException("Provider did not return an identifier.");

        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException("Email not received from external provider.");

        // If GivenName/Surname not available, split the full Name claim
        if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts[0];
            lastName = parts.Length > 1 ? parts[1] : null;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName ?? "Google User",
                LastName = lastName,
                ProfilePictureUrl = pictureUrl,
                EmailConfirmed = true
            };

            var createRes = await CreateUserAsync(user);

            if (!createRes.Succeeded)
                throw new BadRequestException(createRes.Errors.Select(e => e.Description).FirstOrDefault()!);

            await AssignUserToRoleAsync(user, StaticData.CustomerRoleName);

            var customer = new Customer()
            {
                ApplicationUserId = user.Id,
            };
            await customerService.CreateAsync(customer);
        }
        else
        {
            // Prevent account takeover: only allow linking if this provider is already linked
            var alreadyLinked = await IsLoginLinkedAsync(user.Id, provider, externalId);
            if (!alreadyLinked)
            {
                throw new BadRequestException(
                    "An account with this email already exists. Please sign in with your password first, then link your Google account.");
            }

            // Update profile info from provider if missing
            var updated = false;
            if (string.IsNullOrWhiteSpace(user.FirstName) || user.FirstName == "Unknown" || user.FirstName == "Google User")
            {
                user.FirstName = firstName ?? user.FirstName;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(lastName))
            {
                user.LastName = lastName;
                updated = true;
            }
            if (string.IsNullOrWhiteSpace(user.ProfilePictureUrl) && !string.IsNullOrWhiteSpace(pictureUrl))
            {
                user.ProfilePictureUrl = pictureUrl;
                updated = true;
            }
            if (updated)
                await userManager.UpdateAsync(user);
        }

        var loginInfo = new UserLoginInfo(provider, externalId, provider);

        var alreadyLinkedFinal = await
            IsLoginLinkedAsync(user.Id, provider, externalId);

        if (!alreadyLinkedFinal)
        {
            var addLoginResult = await AddLoginAsync(user, loginInfo);
            if (!addLoginResult.Succeeded)
                throw new BadRequestException(addLoginResult.Errors.First().Description);
        }

        var loginResponse = await accountHelper
            .GenerateAndStoreTokensAsync(user, Guid.NewGuid());

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
