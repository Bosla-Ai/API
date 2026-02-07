using BoslaAPI.Middlewares;

namespace BoslaAPI.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseTokenRefresh(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TokenRefreshMiddleware>();
    }
}
