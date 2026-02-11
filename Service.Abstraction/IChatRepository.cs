using Shared.DTOs;

namespace Service.Abstraction;

public interface IChatRepository
{
    Task AddMessageAsync(ChatMessageEntity message);
    Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 15);
    Task DeleteMessagesAsync(string userId, string sessionId, IEnumerable<string> messageIds);

    Task<List<ChatMessageEntity>> GetAllUserMessagesAsync(string userId);

    Task TouchSessionAsync(string userId, string sessionId);

    Task<int> DeleteSessionAsync(string userId, string sessionId);

    Task<int> DeleteInactiveMessagesAsync(DateTime createdBefore, DateTime accessedBefore);
}
