using System.Net;
using System.Security.Claims;
using Domain.Entities;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using AutoMapper;
using Domain.Requests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Abstraction;
using Service.Helpers;
using Shared;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;
using Shared.Parameters;

namespace Presintation.Controllers;

public class AuthenticationController(IServiceManager serviceManager, AuthenticationHelper accountHelper,IMapper mapper,IConfiguration configuration)
    : ApiController
{
    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("GoogleSignIn")]
    public IActionResult GoogleSignIn(string returnUrl = "/")
    {
        try
        {
            var state = Guid.NewGuid().ToString();
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("ExternalCallback",
                    "Authentication", new { provider = "Google", returnUrl }
                    , Request.Scheme),
                Items = { ["state"] = state }
            };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError
                , new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    ErrorMessages = new List<string>() { ex.Message }
                });
        }
    }

    [HttpGet("signin-google")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<APIResponse>> ExternalCallback(string provider, string returnUrl = "/")
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(provider);
            if (!result.Succeeded)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "External authentication failed." }
                });
            }

            var externalId = result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = result.Principal?.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrWhiteSpace(externalId))
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "Provider did not return an identifier." }
                });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "Email not received from external provider." }
                });
            }

            var user = await serviceManager.Authentication.GetUserByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = "Unknown",
                    EmailConfirmed = true
                };

                var createRes = await serviceManager.Authentication
                    .CreateUserAsync(user);

                if (!createRes.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new APIResponse()
                        {
                            IsSuccess = false,
                            StatusCode = HttpStatusCode.InternalServerError,
                            ErrorMessages = createRes.Errors.Select(e => e.Description).ToList()
                        });
                }

                var customer = new Customer()
                {
                    ApplicationUserId = user.Id,
                };
                await serviceManager.Customer.CreateAsync(customer);
                await serviceManager.SaveChangesAsync();
            }

            var loginInfo = new UserLoginInfo(provider, externalId, provider);

            var alreadyLinked = await serviceManager.Authentication
                .IsLoginLinkedAsync(user.Id, provider, externalId);
            if (!alreadyLinked)
            {
                var addLoginResult = await serviceManager.Authentication
                    .AddLoginAsync(user, loginInfo);
                if (addLoginResult == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError
                        , new APIResponse()
                        {
                            IsSuccess = false,
                            StatusCode = HttpStatusCode.InternalServerError,
                            ErrorMessages = new List<string>() { "AddLoginAsync returned null (check implementation)." }
                        });
                }

                if (addLoginResult is IdentityResult identityResult && !identityResult.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError
                        , new APIResponse()
                        {
                            IsSuccess = false,
                            StatusCode = HttpStatusCode.InternalServerError,
                            ErrorMessages = identityResult.Errors.Select(e => e.Description).ToList()
                        });
                }
            }

            var (loginResponse, refreshEntity) =
                await accountHelper.GenerateAndStoreTokensAsync(user, Guid.NewGuid());

            Response.Cookies.Append(StaticData.AccessToken, loginResponse.AccessToken, new CookieOptions()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });
            Response.Cookies.Append(StaticData.RefreshToken, loginResponse.RefreshToken, new CookieOptions()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });

            await serviceManager.RefreshToken.CreateAsync(refreshEntity);
            await serviceManager.SaveChangesAsync();

            return Ok(new APIResponse<LoginResponse>()
            {
                StatusCode = HttpStatusCode.OK,
                Data = loginResponse
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError
                , new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    ErrorMessages = new List<string>() { ex.Message }
                });
        }
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("RegisterCustomer")]
    public async Task<ActionResult<APIResponse>> RegisterCustomer([FromBody] CustomerRegisterDTO customerDTO)
    {
        try
        {
            if (customerDTO == null)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "CustomerDTO is null" }
                });
            }

            var userExists = await serviceManager.Authentication
                .GetUserByEmailAsync(customerDTO.Email);
            if (userExists != null)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "This email is already registered!" }
                });
            }

            var customerUser = mapper.Map<ApplicationUser>(customerDTO);
            var customerUserCreationResult = await serviceManager.Authentication
                .CreateUserAsync(customerUser, customerDTO.Password);
            if (!customerUserCreationResult.Succeeded)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = accountHelper.AddIdentityErrors(customerUserCreationResult)
                });
            }

            var customerRoleAssigningResult =
                await serviceManager.Authentication.AssignUserToRoleAsync(customerUser, StaticData.CustomerRoleName);
            if (!customerRoleAssigningResult.Succeeded)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = accountHelper.AddIdentityErrors(customerRoleAssigningResult)
                });
            }

            var customer = mapper.Map<Customer>(customerDTO);
            customer.ApplicationUserId = customerUser.Id;
            await serviceManager.Customer.CreateAsync(customer);
            await serviceManager.SaveChangesAsync();

            return Ok(new APIResponse()
            {
                StatusCode = HttpStatusCode.OK,
                Data = $"User {customerUser.UserName} Registered Successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { ex.Message }
            });
        }
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("Login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginDTO loginDto)
    {
        var response = await serviceManager.Authentication.LoginAsync(loginDto);
        Response.Cookies.Append(StaticData.AccessToken, response.AccessToken, new CookieOptions()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
        });
        Response.Cookies.Append(StaticData.RefreshToken, response.RefreshToken, new CookieOptions()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
        });
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("LogoutThisDevice")]
    [Authorize]
    public async Task<ActionResult<APIResponse>> LogoutThisDevice([FromBody] LogoutRequest logoutRequest)
    {
        try
        {
            var token = await serviceManager.RefreshToken
                .GetWithDeviceIdNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = logoutRequest.DeviceId
                });

            if (token == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "Invalid Logout" }
                });
            }

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "User logged out";
            await serviceManager.RefreshToken.UpdateAsync(token);
            await serviceManager.SaveChangesAsync();

            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);

            return Ok(new APIResponse()
            {
                StatusCode = HttpStatusCode.OK,
            });
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { ex.Message }
            });
        }
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("LogoutAllDevices")]
    [Authorize]
    public async Task<ActionResult<APIResponse>> LogoutAllDevices([FromBody] LogoutForAllRequest logoutRequest)
    {
        try
        {
            var token = await serviceManager.RefreshToken
                .GetWithDeviceIdNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = logoutRequest.DeviceId
                });

            if (token == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "No active tokens found" }
                });
            }

            var tokens = await serviceManager.RefreshToken
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    UserId = logoutRequest.UserId
                });

            if (tokens == null || !tokens.Any())
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "Invalid Request" }
                });
            }

            foreach (var t in tokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedReason = "User logged out for all active devices";
                await serviceManager.RefreshToken.UpdateAsync(t);
            }

            await serviceManager.SaveChangesAsync();

            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);

            return Ok(new APIResponse()
            {
                StatusCode = HttpStatusCode.OK,
            });
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { ex.Message }
            });
        }
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("RefreshToken")]
    public async Task<ActionResult<APIResponse>> Refresh([FromBody] RefreshRequest model)
    {
        try
        {
            if (model.DeviceId == null || string.IsNullOrWhiteSpace(model.RefreshToken))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "DeviceId or RefreshToken are required" }
                });
            }

            var storedTokens = await serviceManager.RefreshToken
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = model.DeviceId
                });
            if (storedTokens == null || !storedTokens.Any())
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "Invalid RefreshToken" }
                });
            }

            RefreshToken validToken = null;
            foreach (var token in storedTokens)
            {
                if (accountHelper.VerifyTokenWithSalt(model.RefreshToken, token.TokenHash, token.TokenSalt))
                {
                    validToken = token;
                    break;
                }
            }

            if (validToken == null)
            {
                try
                {
                    var firstToken = storedTokens.First();
                    var allUserTokens = await serviceManager.RefreshToken
                        .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                        {
                            UserId = firstToken.UserId
                        });
                    foreach (var t in allUserTokens)
                    {
                        t.IsRevoked = true;
                        t.RevokedAt = DateTime.UtcNow;
                        t.RevokedReason = "Refresh token reuse detected";
                        await serviceManager.RefreshToken.UpdateAsync(t);
                    }

                    await serviceManager.SaveChangesAsync();
                }
                catch (Exception)
                {
                    return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
                    {
                        IsSuccess = false,
                        StatusCode = HttpStatusCode.InternalServerError,
                        ErrorMessages = new List<string>() { "Invalid RefreshToken" }
                    });
                }

                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "Invalid RefreshToken" }
                });
            }

            if (validToken.IsRevoked || validToken.ExpiresAt < DateTime.UtcNow)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "Invalid RefreshToken" }
                });
            }

            var user = await serviceManager.Authentication.GetUserByIdAsync(validToken.UserId);
            if (user == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() { "Invalid User" }
                });
            }

            var newPlain = accountHelper.GeneratePlainRefreshToken();
            var (newHash, newSalt) = accountHelper.CreateTokenHashAndSalt(newPlain);
            var roles = await serviceManager.Authentication.GetRolesAsync(user);
            var (newJwt, newJwtToken) = accountHelper.GenerateJwtAccessToken(user, roles);

            validToken.TokenHash = newHash;
            validToken.TokenSalt = newSalt;
            validToken.Created = DateTime.UtcNow;
            validToken.ExpiresAt =
                DateTime.UtcNow.AddDays(Convert.ToDouble(configuration["JWT:RefreshTokenLifeTime"]!));
            validToken.JwtTokenId = newJwtToken.Id;
            validToken.IsRevoked = false;
            validToken.RevokedAt = null;
            validToken.RevokedReason = null;

            await serviceManager.RefreshToken.UpdateAsync(validToken);
            await serviceManager.SaveChangesAsync();

            var loginResponse = new LoginResponse()
            {
                AccessToken = newJwt,
                AccessTokenExpiration = newJwtToken.ValidTo,
                RefreshToken = newPlain,
                RefreshTokenExpiration = validToken.ExpiresAt,
                UserName = user.UserName!,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? "",
                DeviceId = validToken.DeviceId
            };

            return Ok(new APIResponse<LoginResponse>()
            {
                StatusCode = HttpStatusCode.OK,
                Data = loginResponse
            });
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { ex.Message }
            });
        }
    }
}