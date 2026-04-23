using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class NullFeedbackRepository(ILogger<NullFeedbackRepository> logger) : IFeedbackRepository
{
    public Task SubmitAsync(FeedbackEntity feedback)
    {
        logger.LogWarning("Feedback discarded — Cosmos DB not configured. Rating: {Rating}, Session: {Session}",
            feedback.Rating, feedback.SessionId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FeedbackEntity>> GetBySessionAsync(string userId, string sessionId)
    {
        return Task.FromResult<IReadOnlyList<FeedbackEntity>>(Array.Empty<FeedbackEntity>());
    }

    public Task<IReadOnlyList<FeedbackEntity>> GetAllAsync(int? limit = null)
    {
        return Task.FromResult<IReadOnlyList<FeedbackEntity>>(Array.Empty<FeedbackEntity>());
    }
}
