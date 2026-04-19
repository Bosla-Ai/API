using Domain.Responses;
using Shared.DTOs;

namespace Service.Abstraction;

public interface IFeedbackService
{
    Task<APIResponse<bool>> SubmitFeedbackAsync(string userId, SubmitFeedbackRequest request);
    Task<APIResponse<IReadOnlyList<FeedbackEntity>>> GetSessionFeedbackAsync(string userId, string sessionId);
    Task<APIResponse<IReadOnlyList<FeedbackEntity>>> GetAllFeedbackAsync(int? limit = null);
}
