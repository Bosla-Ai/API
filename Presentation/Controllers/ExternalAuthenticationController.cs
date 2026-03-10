using System.Net;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared.Options;

namespace Presentation.Controllers;

public class ExternalAuthenticationController(
    IServiceManager serviceManager,
    IAuthTicketStore authTicketStore,
    IOptions<OAuthSettingsOptions> oauthOptions,
    IOptions<CookieSettingsOptions> cookieOptions) : ApiController(cookieOptions)
{
    private readonly OAuthSettingsOptions _oauthSettings = oauthOptions.Value;

    /// <summary>
    /// Validates the returnUrl to prevent Open Redirect attacks.
    /// Only allows URLs whose host exactly matches a trusted domain configured in appsettings.
    /// </summary>
    private string ValidateReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return _oauthSettings.DefaultReturnUrl;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return _oauthSettings.DefaultReturnUrl;

        var allowedOrigins = new[] { _oauthSettings.AllowedDomain, _oauthSettings.AlternateDomain }
            .Where(d => !string.IsNullOrEmpty(d));

        foreach (var origin in allowedOrigins)
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var allowedUri)
                && string.Equals(uri.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase))
                return returnUrl;
        }

        return _oauthSettings.DefaultReturnUrl;
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
                    [ex.Message]));
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
            var ticket = await authTicketStore.StoreTicketAsync(response);
            var delimiter = safeReturnUrl.Contains('?') ? "&" : "?";
            return Redirect($"{safeReturnUrl}{delimiter}ticket={ticket}");
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
                    [ex.Message]));
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
            var ticket = await authTicketStore.StoreTicketAsync(response);
            var delimiter = safeReturnUrl.Contains('?') ? "&" : "?";
            return Redirect($"{safeReturnUrl}{delimiter}ticket={ticket}");
        }

        return Redirect(safeReturnUrl);
    }
}