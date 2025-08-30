using System.Net;
using System.Text;
using System.Text.Json;
using Domain.Exceptions;
using Domain.Responses;

namespace BoslaAPI.Middlewares;

public class APIResponseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<APIResponseMiddleware> _logger;

    public APIResponseMiddleware(
        RequestDelegate next,
        ILogger<APIResponseMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var originalBody = context.Response.Body;

        using var newBody = new MemoryStream();
        context.Response.Body = newBody;

        try
        {
            await _next(context);

            newBody.Seek(0, SeekOrigin.Begin);
            var bodyText = await new StreamReader(newBody).ReadToEndAsync();

            newBody.Seek(0, SeekOrigin.Begin);

            object? data = null;
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                try
                {
                    data = JsonSerializer.Deserialize<object>(bodyText);
                }
                catch
                {
                    data = bodyText;
                }
            }

            var apiResponse = new APIResponse
            {
                IsSuccess = context.Response.StatusCode < 400,
                StatusCode = (HttpStatusCode)context.Response.StatusCode,
                Data = data
            };

            var wrappedJson = JsonSerializer.Serialize(apiResponse);
            var bytes = Encoding.UTF8.GetBytes(wrappedJson);

            context.Response.Body = originalBody; 
            context.Response.ContentType = "application/json";
            context.Response.ContentLength = bytes.Length;

            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
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

        var json = JsonSerializer.Serialize(response);
        context.Response.ContentLength = Encoding.UTF8.GetByteCount(json);

        return context.Response.WriteAsync(json);
    }
}