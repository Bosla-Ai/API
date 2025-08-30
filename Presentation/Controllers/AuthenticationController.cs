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
        var response = await serviceManager.Authentication
            .LogoutThisDeviceAsync(logoutRequest);
        if (response != null)
        {
            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);
        }
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("LogoutAllDevices")]
    [Authorize]
    public async Task<ActionResult<APIResponse>> LogoutAllDevices([FromBody] LogoutForAllRequest logoutRequest)
    {
        var response = await serviceManager.Authentication
            .LogoutAllDevicesAsync(logoutRequest);
        if (response != null)
        {
            Response.Cookies.Delete(StaticData.AccessToken);
            Response.Cookies.Delete(StaticData.RefreshToken);
        }
        return Ok(response);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("RefreshToken")]
    public async Task<ActionResult<APIResponse<LoginResponse>>> Refresh([FromBody] RefreshRequest refreshRequest)
    {
        var response = await serviceManager.Authentication
            .RefreshAsync(refreshRequest);
        
        if (response != null)
        {
            Response.Cookies.Append(StaticData.AccessToken, response.Data.AccessToken, new CookieOptions()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });
            Response.Cookies.Append(StaticData.RefreshToken, response.Data.RefreshToken, new CookieOptions()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });
        }
        
        return Ok(response);
    }
}