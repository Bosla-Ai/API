using System.Security.Claims;
using System.Threading.RateLimiting;

namespace BoslaAPI.Extensions;

public static class RateLimiterExtension
{
    public static IServiceCollection AddRateLimiterConfiguration(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Auth endpoints: 5 req/min per IP 
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

            // AI chat endpoints: 5 req/min per user
            options.AddPolicy("AiPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserId(httpContext) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // SSE stream endpoints: 5 req/min per user 
            options.AddPolicy("StreamPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserId(httpContext) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // General authenticated endpoints: 30 req/min per user 
            options.AddPolicy("GeneralPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserId(httpContext) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Roadmap generation: 5 req/day per user 
            options.AddPolicy("RoadmapPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserId(httpContext) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromDays(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Admin endpoints: 60 req/min per user
            options.AddPolicy("AdminPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetUserId(httpContext) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Global fallback: 100 req/min per IP (catch-all) 
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Rejection response
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var userId = GetUserId(context.HttpContext) ?? "anonymous";
                var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var endpoint = context.HttpContext.Request.Path;
                var policy = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? retryAfter
                    : TimeSpan.FromMinutes(1);

                // Abuse logging
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");
                logger.LogWarning(
                    "Rate limit exceeded — User: {UserId}, IP: {IP}, Endpoint: {Endpoint}, RetryAfter: {RetryAfter}s",
                    userId, ip, endpoint, policy.TotalSeconds);

                // Rate limit response headers
                context.HttpContext.Response.Headers["Retry-After"] = ((int)policy.TotalSeconds).ToString();
                context.HttpContext.Response.ContentType = "application/json";

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    statusCode = 429,
                    isSuccess = false,
                    errorMessages = new[] { $"Too many requests. Please retry after {(int)policy.TotalSeconds} seconds." }
                }, cancellationToken);
            };
        });
        return services;
    }

    private static string? GetUserId(HttpContext httpContext)
    {
        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? httpContext.User.FindFirst("sub")?.Value;
    }
}
