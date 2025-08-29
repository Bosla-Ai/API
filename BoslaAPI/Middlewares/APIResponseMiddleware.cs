using System.Net;
using System.Text.Json;
using Domain.Exceptions;
using Domain.Responses;

namespace BoslaAPI.Middlewares;

public class APIResponseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<APIResponseMiddleware> _logger;

    public APIResponseMiddleware(
        RequestDelegate next
        , ILogger<APIResponseMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            var originalBody = context.Response.Body;

            using (var newBody = new MemoryStream())
            {
                context.Response.Body = newBody;

                await _next(context); 

                newBody.Seek(0, SeekOrigin.Begin);
                var bodyText = await new StreamReader(newBody).ReadToEndAsync();
                newBody.Seek(0, SeekOrigin.Begin);

                object data = string.IsNullOrWhiteSpace(bodyText) ? null :
                              JsonSerializer.Deserialize<object>(bodyText);

                var apiResponse = new APIResponse<object>
                {
                    IsSuccess = context.Response.StatusCode < 400,
                    StatusCode = (HttpStatusCode)context.Response.StatusCode,
                    Data = data
                };

                context.Response.ContentType = "application/json";
                var wrappedJson = JsonSerializer.Serialize(apiResponse);
                await context.Response.WriteAsync(wrappedJson);
            }
        }
        catch (BadRequestException ex)
        {
            await HandleExceptionAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            await HandleExceptionAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, "Something went wrong.");
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new APIResponse
        {
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorMessages = new List<string> { message }
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}