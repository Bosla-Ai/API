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

    private CookieOptions GetCookieOptions(DateTime expires)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(_cookieSettings.SameSite, out var mode)
            ? mode
            : SameSiteMode.None;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookieSettings.Secure,
            SameSite = sameSite,
            Expires = expires,
        };

        if (_cookieSettings.Partitioned)
        {
            options.Extensions.Add("Partitioned");
        }

        if (!string.IsNullOrEmpty(_cookieSettings.AllowedSubDomain))
        {
            options.Domain = _cookieSettings.AllowedSubDomain;
        }

        return options;
    }

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
                    var accessTokenOpts = GetCookieOptions(loginServerResponse.AccessTokenExpiration);
                    var refreshTokenOpts = GetCookieOptions(loginServerResponse.RefreshTokenExpiration);

                    context.Response.Cookies.Append(
                        StaticData.AccessToken,
                        loginServerResponse.AccessToken,
                        accessTokenOpts);

                    context.Response.Cookies.Append(
                        StaticData.RefreshToken,
                        loginServerResponse.RefreshToken,
                        refreshTokenOpts);

                    context.Response.Cookies.Append(
                        StaticData.DeviceId,
                        loginServerResponse.DeviceId.ToString(),
                        refreshTokenOpts);

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
