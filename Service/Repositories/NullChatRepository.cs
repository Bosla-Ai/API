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
        // No-op: CosmosDB is not configured
        return Task.CompletedTask;
    }

    public Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 15)
    {
        // Return empty list when CosmosDB is not configured
        return Task.FromResult(new List<ChatMessageEntity>());
    }

    public Task DeleteMessagesAsync(string userId, string sessionId, IEnumerable<string> messageIds)
    {
        // No-op: CosmosDB is not configured
        return Task.CompletedTask;
    }
}
