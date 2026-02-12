using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Exceptions;
using Domain.Responses;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BoslaAPI.Middlewares;

public class ApiResponseMiddleware(RequestDelegate next, ILogger<ApiResponseMiddleware> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task InvokeAsync(HttpContext context)
    {
        // Bypass middleware for streaming endpoints
        if (context.Request.Path.StartsWithSegments("/api/User/ask-ai-with-intent/stream"))
        {
            await next.Invoke(context);
            return;
        }

        var originalBody = context.Response.Body;

        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next.Invoke(context);
            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                context.Response.Body = originalBody;
                await HandelNotFoundAsync(context);
                return;
            }

            buffer.Seek(0, SeekOrigin.Begin);
            var bodyText = await new StreamReader(buffer).ReadToEndAsync();

            // Restore original stream so we can write final bytes to it
            context.Response.Body = originalBody;

            // If response is not JSON (based on content-type) => return original bytes unchanged
            var contentType = context.Response.ContentType ?? string.Empty;
            if (!contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) &&
                context.Response.StatusCode != StatusCodes.Status401Unauthorized &&
                context.Response.StatusCode != StatusCodes.Status403Forbidden)
            {
                // write back raw original buffer
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);
                return;
            }

            // handle empty body (e.g., 204) — we still return a wrapped response or empty depending on your policy
            if (string.IsNullOrWhiteSpace(bodyText) && context.Response.StatusCode == StatusCodes.Status204NoContent)
            {
                context.Response.ContentLength = 0;
                return;
            }

            // Try parse JSON to decide if already an APIResponse (avoid double wrap)
            bool isAlreadyWrapped = false;
            object? data = null;

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                try
                {
                    using var doc = JsonDocument.Parse(bodyText);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        // check for typical APIResponse shape (case-insensitive check via camelCase expected)
                        if (root.TryGetProperty("statusCode", out _) ||
                            root.TryGetProperty("isSuccess", out _) ||
                            root.TryGetProperty("data", out _))
                        {
                            isAlreadyWrapped = true;
                        }
                        else
                        {
                            // keep the object as data (deserialize to object)
                            data = JsonSerializer.Deserialize<object>(bodyText, _jsonOptions);
                        }
                    }
                    else
                    {
                        // if root isn't object (e.g., string/array/number) pass it as-is
                        data = JsonSerializer.Deserialize<object>(bodyText, _jsonOptions) ?? bodyText;
                    }
                }
                catch
                {
                    // not valid JSON => keep raw string
                    data = bodyText;
                }
            }

            if (isAlreadyWrapped)
            {
                // return original JSON unchanged
                var originalBytes = Encoding.UTF8.GetBytes(bodyText);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = originalBytes.Length;
                await originalBody.WriteAsync(originalBytes, 0, originalBytes.Length);
                return;
            }

            // Build the wrapped APIResponse<object>
            var apiResponse = new APIResponse<object>((HttpStatusCode)context.Response.StatusCode, data);

            var wrappedJson = JsonSerializer.Serialize(apiResponse, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(wrappedJson);

            context.Response.ContentType = "application/json";
            context.Response.ContentLength = bytes.Length;
            await originalBody.WriteAsync(bytes, 0, bytes.Length);
        }
        catch (BadRequestException ex)
        {
            context.Response.Body = originalBody;
            await HandleExceptionAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            context.Response.Body = originalBody;
            await HandleExceptionAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (NotFoundException ex)
        {
            context.Response.Body = originalBody;
            await HandleExceptionAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (InternalServerErrorException ex)
        {
            context.Response.Body = originalBody;
            await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, ex.Message);
        }
        catch (Exception ex)
        {
            context.Response.Body = originalBody;
            logger.LogError(ex, "Unhandled exception in APIResponseMiddleware");
            await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, "Something went wrong.");
        }
    }

    private Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new APIResponse<object>((HttpStatusCode)statusCode, null, new List<string> { message });

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        context.Response.ContentLength = Encoding.UTF8.GetByteCount(json);

        return context.Response.WriteAsync(json);
    }

    private async Task HandelNotFoundAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/json";
        var response = new APIResponse()
        {
            StatusCode = (HttpStatusCode)StatusCodes.Status404NotFound,
            ErrorMessages = new List<string>() { $"This End Point {httpContext.Request.Path} was not found." }
        };
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}