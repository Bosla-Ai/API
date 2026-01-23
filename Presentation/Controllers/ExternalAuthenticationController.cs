using System.Net;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared;

namespace Presentation.Controllers;

public class ExternalAuthenticationController(
    IServiceManager serviceManager,
    IConfiguration configuration) : ApiController(configuration)
{
    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("GitHubSignIn")]
    public IActionResult GitHubSignIn(string returnUrl = "https://front.bosla.almiraj.xyz/")
    {
        try
        {
            var state = Guid.NewGuid().ToString();
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GitHubExternalCallback",
                    "ExternalAuthentication", new { returnUrl }
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

    [HttpGet("github-callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<APIResponse>> GitHubExternalCallback(string returnUrl = "https://front.bosla.almiraj.xyz/")
    {
        var result = await HttpContext.AuthenticateAsync("Identity.External");
        if (!result.Succeeded)
            throw new BadRequestException("GitHub authentication failed.");

        var response = await serviceManager.Authentication
            .GitHubLoginAsync(result.Principal, "Github", returnUrl);

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
    public IActionResult GoogleSignIn(string returnUrl = "https://front.bosla.almiraj.xyz/")
    {
        try
        {
            var state = Guid.NewGuid().ToString();
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleExternalCallback",
                    "ExternalAuthentication", new { returnUrl }
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

    [HttpGet("google-callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<APIResponse>> GoogleExternalCallback(string returnUrl = "https://front.bosla.almiraj.xyz/")
    {
        var result = await HttpContext.AuthenticateAsync("Identity.External");
        if (!result.Succeeded)
            throw new BadRequestException("External authentication failed.");

        var response = await serviceManager.Authentication
            .GoogleLoginAsync(result.Principal, "Google", returnUrl);

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