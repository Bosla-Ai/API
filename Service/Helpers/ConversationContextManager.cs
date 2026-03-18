using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared.DTOs;
using Shared.Options;

namespace Service.Helpers;

public class ConversationContextManager(IMemoryCache cache, IChatRepository chatRepository, CustomerHelper customerHelper, IOptionsMonitor<AiOptions> aiOptions)
{
    private readonly IMemoryCache _cache = cache;
    private readonly IChatRepository _chatRepository = chatRepository;
    private readonly CustomerHelper _customerHelper = customerHelper;
    private readonly TimeSpan _hotCacheExpiration = TimeSpan.FromMinutes(10);
    private int CompactionThreshold => aiOptions.CurrentValue.Llm.SummarizationThreshold;

    public Action<string, object>? OnSseEvent { get; set; }

    public async Task AddMessageToContextAsync(string userId, string sessionId, string message, string role = "user")
    {
        var entity = new ChatMessageEntity
        {
            UserId = userId,
            SessionId = sessionId,
            Message = message,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        await _chatRepository.AddMessageAsync(entity);

        var key = GetCacheKey(userId, sessionId);
        _cache.Remove(key);
    }

    public async Task<string> GetConversationContextAsync(string userId, string sessionId)
    {
        var key = GetCacheKey(userId, sessionId);
        var cachedContext = _cache.Get<string>(key);

        if (!string.IsNullOrEmpty(cachedContext))
        {
            return cachedContext;
        }

        var dbMessages = await _chatRepository
            .GetMessagesAsync(userId, sessionId, CompactionThreshold + 5);

        if (!dbMessages.Any())
        {
            return string.Empty;
        }

        var regularMessages = dbMessages.Where(m => m.Role != "summary").ToList();
        var existingCompactContext = dbMessages.FirstOrDefault(m => m.Role == "summary");

        if (regularMessages.Count >= CompactionThreshold && existingCompactContext == null)
        {
            await CompactConversationContextAsync(userId, sessionId, regularMessages);
            dbMessages = await _chatRepository.GetMessagesAsync(userId, sessionId, CompactionThreshold + 5);
        }

        var sb = new StringBuilder();

        var compactContext = dbMessages.FirstOrDefault(m => m.Role == "summary");
        if (compactContext != null)
        {
            sb.AppendLine($"[Previous Compacted Context]: {compactContext.Message}");
            sb.AppendLine();
        }

        var recentMessages = dbMessages.Where(m => m.Role != "summary").ToList();
        if (recentMessages.Any())
        {
            sb.AppendLine("Recent Conversation:");
            foreach (var msg in recentMessages)
            {
                sb.AppendLine($"[{msg.Role}]: {msg.Message}");
            }
        }

        var context = sb.ToString();
        _cache.Set(key, context, _hotCacheExpiration);

        return context;
    }

    private async Task CompactConversationContextAsync(string userId, string sessionId, List<ChatMessageEntity> messages)
    {
        OnSseEvent?.Invoke("tool", new { name = "Compaction", state = "start", summary = $"Compacting {messages.Count} messages into context memory..." });

        var conversationText = new StringBuilder();
        foreach (var msg in messages)
        {
            conversationText.AppendLine($"[{msg.Role}]: {msg.Message}");
        }

        var compactContext = await _customerHelper.CompactConversationAsync(conversationText.ToString());

        var summaryEntity = new ChatMessageEntity
        {
            UserId = userId,
            SessionId = sessionId,
            Message = compactContext,
            Role = "summary",
            CreatedAt = DateTime.UtcNow
        };
        await _chatRepository.AddMessageAsync(summaryEntity);

        OnSseEvent?.Invoke("tool", new { name = "Compaction", state = "end", summary = "Conversation context compacted." });
    }

    public Task ClearConversationContextAsync(string userId, string sessionId)
    {
        var key = GetCacheKey(userId, sessionId);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    private static string GetCacheKey(string userId, string sessionId) => $"chat_{userId}_{sessionId}";
}
