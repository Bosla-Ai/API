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

public class ExternalAuthenticationController(
    IServiceManager serviceManager) : ApiController
{
    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("LinkedInSignIn")]
    public IActionResult LinkedInSignIn(string returnUrl = "https://www.bosla.almiraj.xyz/")
    {
        var state = Guid.NewGuid().ToString();
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action("LinkedInExternalCallback", "ExternalAuthentication",
                new { provider = "LinkedIn", returnUrl }, Request.Scheme),
            Items = { ["state"] = state }
        };
        return Challenge(props, "LinkedIn");
    }

    [HttpGet("signin-linkedin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<APIResponse>> LinkedInExternalCallback(string provider, string returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
            throw new BadRequestException("LinkedIn authentication failed.");

        // If you add a dedicated LinkedInLoginAsync, call it here.
        // Or reuse a generic ExternalLoginAsync if you refactor.
        var response = await serviceManager.Authentication
            .LinkedInLoginAsync(result.Principal, provider, returnUrl);

        if (response != null)
        {
            var accessTokenOptions = GetCookieOptions(response.AccessTokenExpiration);
            var refreshTokenOptions = GetCookieOptions(response.RefreshTokenExpiration);

            Response.Cookies.Append(StaticData.AccessToken, response.AccessToken, accessTokenOptions);
            Response.Cookies.Append(StaticData.RefreshToken, response.RefreshToken, refreshTokenOptions);
            Response.Cookies.Append(StaticData.DeviceId, Convert.ToString(response.DeviceId)!, refreshTokenOptions);
        }

        return Redirect(returnUrl);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("GitHubSignIn")]
    public IActionResult GitHubSignIn(string returnUrl = "/")
    {
        try
        {
            var state = Guid.NewGuid().ToString();
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GitHubExternalCallback",
                    "ExternalAuthentication", new { provider = "Github", returnUrl }
                    , Request.Scheme),
                Items = { ["state"] = state }
            };
            return Challenge(props, "Github");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError
                , new APIResponse<string>(HttpStatusCode.InternalServerError, null,
                    new List<string> { ex.Message }));
        }
    }

    [HttpGet("signin-github")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<APIResponse>> GitHubExternalCallback(string provider, string returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
            throw new BadRequestException("GitHub authentication failed.");

        var response = await serviceManager.Authentication
            .GitHubLoginAsync(result.Principal, provider, returnUrl);

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

        return Redirect(returnUrl);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("GoogleSignIn")]
    public IActionResult GoogleSignIn(string returnUrl = "https://www.bosla.almiraj.xyz/")
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
    public async Task<ActionResult<APIResponse>> GoogleExternalCallback(string provider, string returnUrl = "https://www.bosla.almiraj.xyz/")
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

        return Redirect(returnUrl);
    }
}