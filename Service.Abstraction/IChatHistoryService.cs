using Domain.Responses;
using Shared.DTOs;

namespace Service.Abstraction;

public interface IChatHistoryService
{
    Task<APIResponse<string>> StartNewSessionAsync(string userId);

    Task<APIResponse<List<ChatSessionSummaryDTO>>> GetUserChatSessionsAsync(string userId);

    Task<APIResponse<ChatSessionMessagesDTO>> GetSessionMessagesAsync(string userId, string sessionId);

    Task<APIResponse<int>> DeleteSessionAsync(string userId, string sessionId);

    Task<APIResponse> CancelRequestAsync(string userId, string sessionId, string? partialResponse = null);

    Task<int> CleanInactiveChatsAsync(int maxAgeDays, int renewalGraceDays);
}
