using System.Security.Claims;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared;
using Shared.Options;
using Shared.DTOs.DashboardDTOs;

namespace Presentation.Controllers;


[Authorize]
public class UserController(
    ILogger<UserController> logger,
    IServiceManager serviceManager,
    IOptions<CookieSettingsOptions> cookieOptions)
    : ApiController(cookieOptions)
{
    private string GetUserId(string? sessionId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (userId != null) return userId;

        throw new UnauthorizedException("User ID not found in token");
    }

    [HttpPost("ask-ai")]
    public async Task<ActionResult<APIResponse<string>>> AskAI([FromBody] AiQueryRequest request)
    {
        var userId = GetUserId(request.SessionId);
        var response = await serviceManager.Customer
            .ProcessUserQueryAsync(userId, request.Query!, request.SessionId);
        return Ok(response);
    }

    [HttpPost("ask-ai-with-intent")]
    public ActionResult<AiRequestIdResponse> StartAIQuery([FromBody] AiQueryRequest request)
    {
        var userId = GetUserId(request.SessionId);
        var requestId = serviceManager.Customer.CreateAiRequest(userId, request);
        return Ok(new AiRequestIdResponse(requestId));
    }

    [HttpGet("ask-ai-with-intent/stream")]
    public async Task StreamAIQuery([FromQuery] string requestId)
    {
        var (userId, request) = serviceManager.Customer.GetAiRequest(requestId);

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in serviceManager.Customer
                .ProcessUserQueryStreamAsync(userId, request.Query!, request.SessionId))
            {
                await Response.WriteAsync($"{chunk}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming AI response for requestId: {RequestId}", requestId);
            await Response.WriteAsync($"event: error\ndata: {{\"message\": \"An error occurred\"}}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    [HttpGet("GetCustomerProfile/{customerId:guid}")]
    public async Task<ActionResult<APIResponse>> GetCustomerProfile(string customerId)
    {
        var response = await serviceManager.Customer
            .GetCustomerProfileAsync(customerId);
        return Ok(response);
    }

    [HttpGet("dashboard/domains")]
    public async Task<ActionResult<APIResponse<IEnumerable<DashboardDomainDTO>>>> GetAllDomainsWithHierarchy(
        [FromQuery] bool? isActive = null)
    {
        var response = await serviceManager.User
            .GetAllDomainsWithHierarchyAsync(isActive);
        return Ok(response);
    }
}