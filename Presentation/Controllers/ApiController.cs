using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Presintation.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class ApiController : ControllerBase
{
    protected CookieOptions GetCookieOptions(DateTime lifeTime)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = lifeTime,
            Domain = ".bosla.almiraj.xyz",
        };
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
        Response.Cookies.Delete(StaticData.AccessToken, new CookieOptions { Domain = ".bosla.almiraj.xyz", Secure = true, SameSite = SameSiteMode.None });
        Response.Cookies.Delete(StaticData.RefreshToken, new CookieOptions { Domain = ".bosla.almiraj.xyz", Secure = true, SameSite = SameSiteMode.None });
        Response.Cookies.Delete(StaticData.DeviceId, new CookieOptions { Domain = ".bosla.almiraj.xyz", Secure = true, SameSite = SameSiteMode.None });
    }
}