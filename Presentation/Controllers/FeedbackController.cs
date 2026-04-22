using System.Net;
using System.Security.Claims;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared;
using Shared.DTOs;
using Shared.Options;

namespace Presentation.Controllers;

[Authorize]
public class FeedbackController(
    IServiceManager serviceManager,
    IOptions<CookieSettingsOptions> cookieOptions)
    : ApiController(cookieOptions)
{
    private string GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (userId != null) return userId;

        throw new UnauthorizedException("User ID claim not found");
    }

    [EnableRateLimiting("AiPolicy")]
    [RequestSizeLimit(2_048)]
    [HttpPost]
    public async Task<ActionResult<APIResponse<bool>>> Submit([FromBody] SubmitFeedbackRequest request)
    {
        var userId = GetUserId();
        var response = await serviceManager.Feedback.SubmitFeedbackAsync(userId, request);
        return Ok(response);
    }

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<APIResponse<IReadOnlyList<FeedbackEntity>>>> GetBySession(string sessionId)
    {
        var userId = GetUserId();
        var response = await serviceManager.Feedback.GetSessionFeedbackAsync(userId, sessionId);
        return Ok(response);
    }

    [HttpGet("all")]
    [Authorize(Roles = StaticData.SuperAdminRoleName)]
    [DisableRateLimiting]
    public async Task<ActionResult<APIResponse<IReadOnlyList<FeedbackEntity>>>> GetAll([FromQuery] int? limit = 100)
    {
        var response = await serviceManager.Feedback.GetAllFeedbackAsync(limit);
        return Ok(response);
    }
}
