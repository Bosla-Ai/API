using System.Security.Claims;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared;
using Shared.DTOs;
using Shared.DTOs.DashboardDTOs;
using Shared.Options;

namespace Presentation.Controllers;


//[Authorize]
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
    public async Task StreamAIQuery([FromQuery] string requestId, CancellationToken cancellationToken)
    {
        var (userId, request) = serviceManager.Customer.GetAiRequest(requestId);

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in serviceManager.Customer
                .ProcessUserQueryStreamAsync(userId, request.Query!, request.SessionId, cancellationToken, request.ChatMode)
                .WithCancellation(cancellationToken))
            {
                await Response.WriteAsync($"{chunk}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — frontend cancel endpoint handles cleanup & DB marker
            logger.LogInformation("Client disconnected SSE stream for requestId: {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming AI response for requestId: {RequestId}", requestId);
            try
            {
                await Response.WriteAsync($"event: error\ndata: {{\"message\": \"An error occurred\"}}\n\n");
                await Response.Body.FlushAsync();
            }
            catch
            {
                // Client already disconnected
            }
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

    [HttpPost("start-chat")]
    public async Task<ActionResult<APIResponse<string>>> StartNewChat()
    {
        var userId = GetUserId();
        var response = await serviceManager.ChatHistory.StartNewSessionAsync(userId);
        return Ok(response);
    }

    [HttpPost("cancel-chat/{sessionId}")]
    public async Task<ActionResult<APIResponse>> CancelChat(string sessionId, [FromBody] CancelChatRequest? request = null)
    {
        var userId = GetUserId();
        var response = await serviceManager.ChatHistory.CancelRequestAsync(userId, sessionId, request?.PartialResponse);
        return Ok(response);
    }

    [HttpGet("chat-history")]
    public async Task<ActionResult<APIResponse<List<ChatSessionSummaryDTO>>>> GetChatHistory()
    {
        var userId = GetUserId();
        var response = await serviceManager.ChatHistory.GetUserChatSessionsAsync(userId);
        return Ok(response);
    }

    [HttpGet("chat-history/{sessionId}")]
    public async Task<ActionResult<APIResponse<ChatSessionMessagesDTO>>> GetChatSession(string sessionId)
    {
        var userId = GetUserId();
        var response = await serviceManager.ChatHistory.GetSessionMessagesAsync(userId, sessionId);
        return Ok(response);
    }

    [HttpDelete("chat-history/{sessionId}")]
    public async Task<ActionResult<APIResponse<int>>> DeleteChatSession(string sessionId)
    {
        var userId = GetUserId();
        var response = await serviceManager.ChatHistory.DeleteSessionAsync(userId, sessionId);
        return Ok(response);
    }
}
