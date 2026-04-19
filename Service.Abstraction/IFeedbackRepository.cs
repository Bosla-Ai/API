using Shared.DTOs;

namespace Service.Abstraction;

public interface IFeedbackRepository
{
    Task SubmitAsync(FeedbackEntity feedback);
    Task<IReadOnlyList<FeedbackEntity>> GetBySessionAsync(string userId, string sessionId);
}
