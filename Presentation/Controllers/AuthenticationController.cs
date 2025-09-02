using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Abstraction;
using Shared;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;

namespace Presintation.Controllers;

public class AuthenticationController(
    IServiceManager serviceManager)
    : ApiController
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
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginDTO loginDto)
    {
        var response = await serviceManager.Authentication.LoginAsync(loginDto);
        if (response != null)
        {
            var accessTokenLifeTime = response.AccessTokenExpiration;
            var refreshTokenLifeTime = response.RefreshTokenExpiration;
            
            var accessTokenOptions = GetCookieOptions(accessTokenLifeTime);
            var refreshTokenOptions = GetCookieOptions(refreshTokenLifeTime);
            
            Response.Cookies.Append(StaticData.AccessToken, response.AccessToken, accessTokenOptions);
            Response.Cookies.Append(StaticData.RefreshToken, response.RefreshToken, refreshTokenOptions);
            Response.Cookies.Append(StaticData.DeviceId, Convert.ToString(response.DeviceId)!, refreshTokenOptions);
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
            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);
            Response.Cookies.Delete(StaticData.DeviceId);
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
            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);
            Response.Cookies.Delete(StaticData.DeviceId);
        }
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("RefreshToken")]
    public async Task<ActionResult<APIResponse<LoginResponse>>> Refresh()
    {
        var refreshToken = Request.Cookies[StaticData.RefreshToken];
        var accessToken = Request.Cookies[StaticData.AccessToken];
        var deviceId = Request.Cookies[StaticData.DeviceId];

        var refreshRequest = new RefreshRequest()
        {
            RefreshToken = refreshToken,
            DeviceId = Guid.TryParse(deviceId, out var parsedDeviceId) ? parsedDeviceId : Guid.Empty
        };
        
        var response = await serviceManager.Authentication
            .RefreshAsync(refreshRequest);
        
        if (response != null)
        {
            var accessTokenLifeTime = response.Data.AccessTokenExpiration;
            var refreshTokenLifeTime = response.Data.RefreshTokenExpiration;
            
            var accessTokenOptions = GetCookieOptions(accessTokenLifeTime);
            var refreshTokenOptions = GetCookieOptions(refreshTokenLifeTime);
            
            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);
            Response.Cookies.Delete(StaticData.DeviceId);

            Response.Cookies.Append(StaticData.AccessToken, response.Data.AccessToken, accessTokenOptions);
            Response.Cookies.Append(StaticData.RefreshToken, response.Data.RefreshToken, refreshTokenOptions);
            Response.Cookies.Append(StaticData.DeviceId, Convert.ToString(response.Data.DeviceId)!, refreshTokenOptions);
        }
        return Ok(response);
    }

    private CookieOptions GetCookieOptions(DateTime lifeTime)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = lifeTime
        };
    }
}