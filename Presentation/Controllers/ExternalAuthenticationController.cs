using System.Net;
using System.Security.Claims;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Abstraction;
using Shared;

namespace Presintation.Controllers;

[Route("api/[controller]")]
public class ExternalAuthenticationController(
    IServiceManager serviceManager) : ApiController
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
                RedirectUri = Url.Action("GoogleExternalCallback",
                    "ExternalAuthentication", new { provider = "Google", returnUrl }
                    , Request.Scheme),
                Items = { ["state"] = state }
            };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError
                , new APIResponse<string>(HttpStatusCode.InternalServerError, null,
                    new List<string> { ex.Message }));
        }
    }

    [HttpGet("signin-google")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<APIResponse>> GoogleExternalCallback(string provider, string returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
            throw new BadRequestException("External authentication failed.");
        
        var response = await serviceManager.Authentication
            .GoogleLoginAsync(result.Principal, provider, returnUrl);

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