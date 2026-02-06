using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class ApiController(IConfiguration configuration) : ControllerBase
{
    protected CookieOptions GetCookieOptions(DateTime lifeTime)
    {
        var domain = configuration["CookieSettings:AllowedSubDomain"];
        var isSecure = bool.TryParse(configuration["CookieSettings:Secure"], out var secure) ? secure : true;
        var sameSite = Enum.TryParse<SameSiteMode>(configuration["CookieSettings:SameSite"], out var mode) ? mode : SameSiteMode.None;
        var isPartitioned = bool.TryParse(configuration["CookieSettings:Partitioned"], out var partitioned) ? partitioned : false;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSite,
            Expires = lifeTime,
        };

        if (isPartitioned)
        {
            options.Extensions.Add("Partitioned");
        }

        // Only set domain if explicitly configured (empty = localhost)
        if (!string.IsNullOrEmpty(domain))
        {
            options.Domain = domain;
        }

        return options;
    }
    protected void SetAuthCookies(LoginServerResponse response)
    {
        var accessTokenOptions = GetCookieOptions(response.AccessTokenExpiration);
        var refreshTokenOptions = GetCookieOptions(response.RefreshTokenExpiration);

        Response.Cookies.Append(StaticData.AccessToken, response.AccessToken, accessTokenOptions);
        Response.Cookies.Append(StaticData.RefreshToken, response.RefreshToken, refreshTokenOptions);
        Response.Cookies.Append(StaticData.DeviceId, response.DeviceId.ToString(), refreshTokenOptions);
    }
    protected void ClearAuthCookies()
    {
        var options = GetCookieOptions(DateTime.UtcNow.AddDays(-1));
        Response.Cookies.Delete(StaticData.AccessToken, options);
        Response.Cookies.Delete(StaticData.RefreshToken, options);
        Response.Cookies.Delete(StaticData.DeviceId, options);
    }
}