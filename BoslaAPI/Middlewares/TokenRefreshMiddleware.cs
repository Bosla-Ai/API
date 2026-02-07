using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Requests;
using Microsoft.IdentityModel.Tokens;
using Service.Abstraction;
using Shared;

namespace BoslaAPI.Middlewares;

public class TokenRefreshMiddleware(RequestDelegate next, IConfiguration configuration)
{
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
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = loginServerResponse.RefreshTokenExpiration
                    };


                    context.Response.Cookies.Append(StaticData.AccessToken, loginServerResponse.AccessToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = loginServerResponse.AccessTokenExpiration
                    });

                    context.Response.Cookies.Append(StaticData.RefreshToken, loginServerResponse.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = loginServerResponse.RefreshTokenExpiration
                    });

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
        var key = Encoding.UTF8.GetBytes(configuration["JWT:Key"]!);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = configuration["JWT:Issuer"],
                ValidateAudience = true,
                ValidAudience = configuration["JWT:Audience"],
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
