using System.Security.Claims;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared;
using Shared.DTOs.ApplicationUserDTOs;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;

namespace Presintation.Controllers;

public class AuthenticationController(
    IServiceManager serviceManager,
    IConfiguration configuration)
    : ApiController(configuration)
{

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("RegisterCustomer")]
    public async Task<ActionResult<APIResponse>> RegisterCustomer([FromBody] CustomerRegisterDTO customerDTO)
    {
        var response = await serviceManager.Authentication
            .RegisterCustomerAsync(customerDTO);
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("Login")]
    public async Task<ActionResult<LoginClientResponse>> Login([FromBody] LoginDTO loginDto)
    {
        var (response, loginServerResponse) = await serviceManager
            .Authentication.LoginAsync(loginDto);

        if (response != null)
        {
            ClearAuthCookies();
            SetAuthCookies(loginServerResponse);
        }
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("LogoutThisDevice")]
    [Authorize]
    public async Task<ActionResult<APIResponse>> LogoutThisDevice()
    {
        var deviceId = Request.Cookies[StaticData.DeviceId];

        var logoutRequest = new LogoutRequest()
        {
            DeviceId = Guid.TryParse(deviceId, out var parsedDeviceId) ? parsedDeviceId : Guid.Empty
        };

        var response = await serviceManager.Authentication
            .LogoutThisDeviceAsync(logoutRequest);

        if (response != null)
        {
            ClearAuthCookies();
        }
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("LogoutAllDevices")]
    [Authorize]
    public async Task<ActionResult<APIResponse>> LogoutAllDevices()
    {
        var deviceId = Request.Cookies[StaticData.DeviceId];

        var logoutRequest = new LogoutForAllRequest()
        {
            DeviceId = Guid.TryParse(deviceId, out var parsedDeviceId) ? parsedDeviceId : Guid.Empty
        };

        var response = await serviceManager.Authentication
            .LogoutAllDevicesAsync(logoutRequest);
        if (response != null)
        {
            ClearAuthCookies();
        }
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("RefreshToken")]
    public async Task<ActionResult<APIResponse<LoginClientResponse>>> Refresh()
    {
        var refreshToken = Request.Cookies[StaticData.RefreshToken];
        var accessToken = Request.Cookies[StaticData.AccessToken];
        var deviceId = Request.Cookies[StaticData.DeviceId];

        var refreshRequest = new RefreshRequest()
        {
            RefreshToken = refreshToken,
            DeviceId = Guid.TryParse(deviceId, out var parsedDeviceId) ? parsedDeviceId : Guid.Empty
        };

        var (response, loginServerResponse) = await serviceManager.Authentication
            .RefreshAsync(refreshRequest);

        if (loginServerResponse != null)
        {
            ClearAuthCookies();
            SetAuthCookies(loginServerResponse);
        }

        return Ok(response);
    }

    [HttpGet("Me")]
    [Authorize]
    public async Task<ActionResult<APIResponse<ApplicationUserDTO>>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var response = await serviceManager.Authentication
            .GetMeAsync(userId!);
        return Ok(response);
    }
}