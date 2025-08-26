using System.Threading.RateLimiting;

namespace BoslaAPI.Extensions;

public static class RateLimiterExtension
{
    public static IServiceCollection AddRateLimiterConfiguration(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("AuthPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,                 
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });
        return services;
    }
}