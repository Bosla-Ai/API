using System.Net;
using Domain.Responses;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Implementations;

public class FeedbackService(
    IFeedbackRepository feedbackRepository,
    ILogger<FeedbackService> logger) : IFeedbackService
{
    public async Task<APIResponse<bool>> SubmitFeedbackAsync(string userId, SubmitFeedbackRequest request)
    {
        var entity = new FeedbackEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            SessionId = request.SessionId,
            MessageId = request.MessageId,
            Rating = request.Rating,
            Comment = request.Comment,
            Reason = request.Reason,
            IntentType = request.IntentType,
            CreatedAt = DateTime.UtcNow
        };

        await feedbackRepository.SubmitAsync(entity);

        logger.LogInformation("Feedback submitted: User={UserId} Session={SessionId} Rating={Rating} Reason={Reason}",
            userId, request.SessionId, request.Rating, request.Reason ?? "none");

        return new APIResponse<bool>(HttpStatusCode.OK, true);
    }

    public async Task<APIResponse<IReadOnlyList<FeedbackEntity>>> GetSessionFeedbackAsync(string userId, string sessionId)
    {
        var feedback = await feedbackRepository.GetBySessionAsync(userId, sessionId);
        return new APIResponse<IReadOnlyList<FeedbackEntity>>(HttpStatusCode.OK, feedback);
    }

    public async Task<APIResponse<IReadOnlyList<FeedbackEntity>>> GetAllFeedbackAsync(int? limit = null)
    {
        var feedback = await feedbackRepository.GetAllAsync(limit);
        return new APIResponse<IReadOnlyList<FeedbackEntity>>(HttpStatusCode.OK, feedback);
    }
}
