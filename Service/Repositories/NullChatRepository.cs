using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

/// <summary>
/// A no-op implementation of IChatRepository used when CosmosDB is not configured.
/// This allows the application to run without CosmosDB for non-AI features.
/// </summary>
public class NullChatRepository : IChatRepository
{
    public Task AddMessageAsync(ChatMessageEntity message)
    {
        return Task.CompletedTask;
    }

    public Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 15)
    {
        return Task.FromResult(new List<ChatMessageEntity>());
    }

    public Task<ChatMessageEntity?> GetLatestSummaryMessageAsync(string userId, string sessionId)
    {
        return Task.FromResult<ChatMessageEntity?>(null);
    }

    public Task<string?> GetLatestStateMessageByPrefixAsync(string userId, string sessionId, string prefix)
    {
        return Task.FromResult<string?>(null);
    }

    public Task DeleteMessagesAsync(string userId, string sessionId, IEnumerable<string> messageIds)
    {
        return Task.CompletedTask;
    }

    public Task<List<ChatMessageEntity>> GetAllUserMessagesAsync(string userId)
    {
        return Task.FromResult(new List<ChatMessageEntity>());
    }

    public Task TouchSessionAsync(string userId, string sessionId)
    {
        return Task.CompletedTask;
    }

    public Task<int> DeleteSessionAsync(string userId, string sessionId)
    {
        return Task.FromResult(0);
    }

    public Task<int> DeleteInactiveMessagesAsync(DateTime createdBefore, DateTime accessedBefore)
    {
        return Task.FromResult(0);
    }
}
