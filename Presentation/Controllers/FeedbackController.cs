using System.Net;
using System.Security.Claims;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared.DTOs;
using Shared.Options;

namespace Presentation.Controllers;

[Authorize]
public class FeedbackController(
    ILogger<FeedbackController> logger,
    IFeedbackRepository feedbackRepository,
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

        var entity = new FeedbackEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            SessionId = request.SessionId,
            MessageId = request.MessageId,
            Rating = request.Rating,
            Comment = request.Comment,
            IntentType = request.IntentType,
            CreatedAt = DateTime.UtcNow
        };

        await feedbackRepository.SubmitAsync(entity);

        logger.LogInformation("Feedback submitted: User={UserId} Session={SessionId} Rating={Rating}",
            userId, request.SessionId, request.Rating);

        return Ok(new APIResponse<bool>(HttpStatusCode.OK, true));
    }

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<APIResponse<IReadOnlyList<FeedbackEntity>>>> GetBySession(string sessionId)
    {
        var userId = GetUserId();
        var feedback = await feedbackRepository.GetBySessionAsync(userId, sessionId);
        return Ok(new APIResponse<IReadOnlyList<FeedbackEntity>>(HttpStatusCode.OK, feedback));
    }
}
