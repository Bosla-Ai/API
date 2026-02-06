using System.Security.Claims;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared;
using Shared.DTOs;
using Shared.DTOs.DashboardDTOs;

namespace Presentation.Controllers;

[Authorize]
public class UserController(
    ILogger<UserController> logger,
    IServiceManager serviceManager,
    IConfiguration configuration)
    : ApiController(configuration)
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
    public async Task<ActionResult<APIResponse<AiIntentDetectionResponse>>> AskAIWithIntent([FromBody] AiQueryRequest request)
    {
        var userId = GetUserId(request.SessionId);
        var response = await serviceManager.Customer
            .ProcessUserQueryWithIntentDetectionAsync(userId, request.Query!, request.SessionId);
        return Ok(response);
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