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
    // Read OAuth settings from configuration - support multiple domains
    private readonly string _defaultReturnUrl = configuration["OAuthSettings:DefaultReturnUrl"] ?? "https://bosla.me/";
    private readonly string[] _allowedDomains = new[]
    {
        configuration["OAuthSettings:AllowedDomain"] ?? "https://bosla.me",
        configuration["OAuthSettings:AlternateDomain"] ?? ""
    }.Where(d => !string.IsNullOrEmpty(d)).ToArray();

    /// <summary>
    /// Validates the returnUrl to prevent Open Redirect attacks.
    /// Only allows URLs that start with the trusted domains configured in appsettings.
    /// </summary>
    private string ValidateReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return _defaultReturnUrl;

        // Prevent open redirect attacks by validating the return URL against all allowed domains
        foreach (var allowedDomain in _allowedDomains)
        {
            if (returnUrl.StartsWith(allowedDomain, StringComparison.OrdinalIgnoreCase))
                return returnUrl;
        }

        return _defaultReturnUrl;
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("GitHubSignIn")]
    public IActionResult GitHubSignIn(string? returnUrl = null)
    {
        try
        {
            // Validate returnUrl to prevent open redirect attacks
            var safeReturnUrl = ValidateReturnUrl(returnUrl);
            var state = Guid.NewGuid().ToString();
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GitHubExternalCallback",
                    "ExternalAuthentication", new { returnUrl = safeReturnUrl }
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
    public async Task<ActionResult<APIResponse>> GitHubExternalCallback(string? returnUrl = null)
    {
        // Validate returnUrl early to get a safe URL
        var safeReturnUrl = ValidateReturnUrl(returnUrl);

        var result = await HttpContext.AuthenticateAsync("Identity.External");
        if (!result.Succeeded)
            throw new BadRequestException("GitHub authentication failed.");

        var response = await serviceManager.Authentication
            .GitHubLoginAsync(result.Principal, "Github", safeReturnUrl);

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

        return Redirect(safeReturnUrl);
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpGet("GoogleSignIn")]
    public IActionResult GoogleSignIn(string? returnUrl = null)
    {
        try
        {
            var safeReturnUrl = ValidateReturnUrl(returnUrl);
            var state = Guid.NewGuid().ToString();
            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleExternalCallback",
                    "ExternalAuthentication", new { returnUrl = safeReturnUrl }
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
    public async Task<ActionResult<APIResponse>> GoogleExternalCallback(string? returnUrl = null)
    {
        // Validate returnUrl early to get a safe URL
        var safeReturnUrl = ValidateReturnUrl(returnUrl);

        var result = await HttpContext.AuthenticateAsync("Identity.External");
        if (!result.Succeeded)
            throw new BadRequestException("External authentication failed.");

        var response = await serviceManager.Authentication
            .GoogleLoginAsync(result.Principal, "Google", safeReturnUrl);

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

        return Redirect(safeReturnUrl);
    }
}