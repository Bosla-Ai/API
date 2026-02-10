using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Requests;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Service.Abstraction;
using Shared;
using Shared.Options;

namespace BoslaAPI.Middlewares;

public class TokenRefreshMiddleware(
    RequestDelegate next,
    IOptions<JwtOptions> jwtOptions,
    IOptions<CookieSettingsOptions> cookieOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly CookieSettingsOptions _cookieSettings = cookieOptions.Value;

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory serviceScopeFactory)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        var refreshToken = context.Request.Cookies[StaticData.RefreshToken];
        var deviceIdString = context.Request.Cookies[StaticData.DeviceId];

        if (!string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(deviceIdString) &&
            Guid.TryParse(deviceIdString, out var deviceId))
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                var refreshRequest = new RefreshRequest
                {
                    RefreshToken = refreshToken,
                    DeviceId = deviceId
                };

                var (apiResponse, loginServerResponse) = await authService.RefreshAsync(refreshRequest);

                if (loginServerResponse != null)
                {
                    context.Response.Cookies.Append(StaticData.AccessToken, loginServerResponse.AccessToken,
                        GetCookieOptions(loginServerResponse.AccessTokenExpiration));

                    context.Response.Cookies.Append(StaticData.RefreshToken, loginServerResponse.RefreshToken,
                        GetCookieOptions(loginServerResponse.RefreshTokenExpiration));

                    context.Response.Cookies.Append(StaticData.DeviceId, deviceId.ToString(),
                        GetCookieOptions(loginServerResponse.RefreshTokenExpiration));

                    var principal = ValidateToken(loginServerResponse.AccessToken);
                    if (principal != null)
                    {
                        context.User = principal;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        await next(context);
    }

    private CookieOptions GetCookieOptions(DateTime expires)
    {
        var domain = _cookieSettings.AllowedSubDomain;
        var isSecure = _cookieSettings.Secure;
        var sameSite = Enum.TryParse<SameSiteMode>(_cookieSettings.SameSite, out var mode) ? mode : SameSiteMode.None;
        var isPartitioned = _cookieSettings.Partitioned;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSite,
            Expires = expires
        };

        if (isPartitioned)
            options.Extensions.Add("Partitioned");

        if (!string.IsNullOrEmpty(domain))
            options.Domain = domain;

        return options;
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtOptions.Key);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtOptions.Audience,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
