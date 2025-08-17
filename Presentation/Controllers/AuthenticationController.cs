using System.Net;
using Domain.Contracts;
using Domain.Entities;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using AutoMapper;
using Domain.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Service.Abstraction;
using Service.Helpers;
using Shared;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;
using Shared.Parameters;

namespace Presintation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;
    private readonly IUnitOfService _unitOfService;
    private readonly AuthenticationHelper _accountHelper;

    public AuthenticationController(
        IConfiguration configuration,
        IMapper mapper,
        IUnitOfService unitOfService,
        AuthenticationHelper accountHelper)
    {
        _configuration = configuration;
        _mapper = mapper;
        _unitOfService = unitOfService;
        _accountHelper = accountHelper;
    }

    [HttpPost("RegisterCustomer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<APIResponse>> RegisterCustomer([FromBody]CustomerRegisterDTO customerDTO)
    {
        try
        {
            if (customerDTO == null)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() {"CustomerDTO is null"}
                });
            }
            var userExists = await _unitOfService.Authentication
                .GetUserByEmailAsync(customerDTO.Email);
            if (userExists != null)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new  List<string>() {"This email is already registered!"}    
                });
            }
            
            var customerUser = _mapper.Map<ApplicationUser>(customerDTO);
            var customerUserCreationResult = await _unitOfService.Authentication.CreateUserAsync(customerUser , customerDTO.Password);
            if (!customerUserCreationResult.Succeeded)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = _accountHelper.AddIdentityErrors(customerUserCreationResult)
                });
            }

            var customerRoleAssigningResult =
                await _unitOfService.Authentication.AssignUserToRoleAsync(customerUser, StaticData.CustomerRoleName);
            if (!customerRoleAssigningResult.Succeeded)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = _accountHelper.AddIdentityErrors(customerUserCreationResult)
                });
            }
            
            var customer = _mapper.Map<Customer>(customerDTO);
            customer.ApplicationUserId = customerUser.Id;
            await _unitOfService.Customer.CreateAsync(customer);
            await _unitOfService.SaveChangesAsync();
            
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
                ErrorMessages = new List<string>() {ex.Message}
            });
        }
    }
    
    [HttpPost("Login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<APIResponse>> Login([FromBody]LoginDTO loginDTO)
    {
        try
        {
            var user = await _unitOfService.Authentication.GetUserByEmailAsync(loginDTO.Email);
            if (user == null || !await _unitOfService.Authentication
                    .CheckPasswordAsync(user, loginDTO.Password))
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() {"Invalid Login"}
                });
            }

            var roles = await _unitOfService.Authentication.GetRolesAsync(user);

            var deviceId = Guid.NewGuid();

            var (jwt, jwtToken) = _accountHelper.GenerateJwtAccessToken(user, roles);

            var plainRefresh = _accountHelper.GeneratePlainRefreshToken();
            var (hash, salt) = _accountHelper.CreateTokenHashAndSalt(plainRefresh);

            var refreshEntity = new RefreshToken
            {
                DeviceId = deviceId,
                TokenHash = hash,
                TokenSalt = salt,
                Created = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow
                    .AddDays(Convert.ToDouble(_configuration["JWT:RefreshTokenLifeTime"])),
                UserId = user.Id,
                JwtTokenId = jwtToken.Id
            };

            var existing =
                await _unitOfService.RefreshToken
                    .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                    {
                        DeviceId = deviceId, UserId = user.Id
                    });
            
            if (existing != null && existing.Any())
            {
                foreach (var item in existing)
                {
                    item.IsRevoked = true;
                    item.RevokedAt = DateTime.UtcNow;
                    item.RevokedReason = "New login from same device";
                    await _unitOfService.RefreshToken.UpdateAsync(item);
                }
            }
            await _unitOfService.RefreshToken.CreateAsync(refreshEntity);
            await _unitOfService.SaveChangesAsync();

            var loginResponse = new LoginResponse()
            {
                AccessToken = jwt,
                AccessTokenExpiration = jwtToken.ValidTo,
                RefreshToken = plainRefresh,
                RefreshTokenExpiration = refreshEntity.ExpiresAt,
                UserName = user.UserName!,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? "",
                DeviceId = deviceId 
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
                ErrorMessages = new List<string>() {ex.Message}
            });
        }
    }

    [HttpPost("LogoutThisDevice")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<APIResponse>> LogoutThisDevice([FromBody]LogoutRequest logoutRequest)
    {
        try
        {
            var token = await _unitOfService.RefreshToken
                .GetWithDeviceIdNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = logoutRequest.DeviceId
                });

            if (token == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>(){"Invalid Logout"}
                });
            }

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "User logged out";
            await _unitOfService.RefreshToken.UpdateAsync(token);
            await _unitOfService.SaveChangesAsync();
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
                ErrorMessages = new List<string>() {ex.Message}
            });
        }
    }

    [HttpPost("LogoutAllDevices")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<APIResponse>> LogoutAllDevices([FromBody]LogoutForAllRequest logoutRequest)
    {
        try
        {
            var token = await _unitOfService.RefreshToken
                .GetWithDeviceIdNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = logoutRequest.DeviceId
                });

            if (token == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>(){"Invalid Request"}
                });
            }

            var tokens = await _unitOfService.RefreshToken
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    UserId = logoutRequest.UserId
                });

            if (tokens == null || !tokens.Any())
            {
                return Ok(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>(){"Invalid Request"}
                });
            }

            foreach (var t in tokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedReason = "User logged out for all active devices";
                await _unitOfService.RefreshToken.UpdateAsync(t);
            }

            await _unitOfService.SaveChangesAsync();
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
                ErrorMessages = new List<string>() {ex.Message}
            });
        }
    }

    // [HttpPost("ResetPassword")]
    // [ProducesResponseType(StatusCodes.Status200OK)]
    // [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    // [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    // public async Task<ActionResult<APIResponse>> ResetPassword([FromBody] ResetPasswordRequest ResetRequest)
    // {
    //     
    // }
    
    [HttpPost("RefreshToken")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<APIResponse>> Refresh([FromBody]RefreshRequest model)
    {
        try
        {
            if (model.DeviceId == null || string.IsNullOrWhiteSpace(model.RefreshToken))
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new  APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() {"DeviceId or RefreshToken are required"}
                });
            }

            var storedTokens = await _unitOfService.RefreshToken
                .GetAllForUserDeviceNotRevokedAsync(new RefreshTokenParameters()
                {
                    DeviceId = model.DeviceId
                });
            if (storedTokens == null || !storedTokens.Any())
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new  APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() {"Invalid RefreshToken"}
                });
            }
            
            RefreshToken validToken = null;
            foreach (var token in storedTokens)
            {
                if (_accountHelper.VerifyTokenWithSalt(model.RefreshToken, token.TokenHash, token.TokenSalt))
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
                    var allUserTokens = await _unitOfService.RefreshToken
                        .GetAllForUserDeviceNotRevokedAsync(new  RefreshTokenParameters()
                        {
                            UserId = validToken!.UserId
                        });
                    foreach (var t in allUserTokens)
                    {
                        t.IsRevoked = true;
                        t.RevokedAt = DateTime.UtcNow;
                        t.RevokedReason = "Refresh token reuse detected";
                        await _unitOfService.RefreshToken.UpdateAsync(t);
                    }
                    await _unitOfService.SaveChangesAsync();
                }
                catch (Exception)
                {
                    return StatusCode((int)HttpStatusCode.InternalServerError, new  APIResponse()
                    {
                        IsSuccess = false,
                        StatusCode = HttpStatusCode.InternalServerError,
                        ErrorMessages = new List<string>() {"Invalid RefreshToken"}
                    });
                }
                return StatusCode(StatusCodes.Status401Unauthorized,new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() {"Invalid RefreshToken"}
                });
            }

            if (validToken.IsRevoked || validToken.ExpiresAt < DateTime.UtcNow)
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() {"Invalid RefreshToken"}
                });
            }
            
            var newPlain = _accountHelper.GeneratePlainRefreshToken();
            var (newHash, newSalt) = _accountHelper.CreateTokenHashAndSalt(newPlain);
            
            var user = await _unitOfService.Authentication.GetUserByIdAsync(validToken.UserId);
            if (user == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized,new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.Unauthorized,
                    ErrorMessages = new List<string>() {"Invalid User"}
                });
            }

            var roles = await _unitOfService.Authentication.GetRolesAsync(user);
            var (newJwt, newJwtToken) = _accountHelper.GenerateJwtAccessToken(user, roles);

            validToken.TokenHash = newHash;
            validToken.TokenSalt = newSalt;
            validToken.Created = DateTime.UtcNow;
            validToken.ExpiresAt = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JWT:RefreshTokenLifeTime"]!));
            validToken.JwtTokenId = newJwtToken.Id;
            validToken.IsRevoked = false; 
            validToken.RevokedAt = null;
            validToken.RevokedReason = null;

            await _unitOfService.RefreshToken.UpdateAsync(validToken);
            await _unitOfService.SaveChangesAsync();

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

            return Ok(new APIResponse()
            {
                StatusCode = HttpStatusCode.OK,
                Data = loginResponse
            });
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, new  APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() {ex.Message}
            });
        }
    }
}