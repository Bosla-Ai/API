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
    private int MaxContextMessageLength => Math.Clamp(aiOptions.CurrentValue.Llm.ContextMaxMessageLength, 1000, 50000);
    private int ContextCompactionCharThreshold => Math.Clamp(aiOptions.CurrentValue.Llm.ContextCompactionCharThreshold, 8000, 300000);

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

        var dbMessages = await _chatRepository.GetMessagesAsync(userId, sessionId, CompactionThreshold + 5);

        if (!dbMessages.Any())
        {
            return string.Empty;
        }

        var regularMessages = dbMessages.Where(m => m.Role != "summary" && m.Role != "title" && m.Role != "state").ToList();
        var existingCompactContext = dbMessages.FirstOrDefault(m => m.Role == "summary");

        if (regularMessages.Count >= CompactionThreshold && existingCompactContext == null)
        {
            await CompactConversationContextAsync(userId, sessionId, regularMessages);
            dbMessages = await _chatRepository.GetMessagesAsync(userId, sessionId, CompactionThreshold + 5);
        }

        var context = BuildContext(dbMessages);

        // Edge-case guard: compact if assembled context exceeds safe threshold.
        if (context.Length > ContextCompactionCharThreshold)
        {
            var messagesToCompact = dbMessages
                .Where(m => m.Role != "summary" && m.Role != "title" && m.Role != "state")
                .OrderBy(m => m.CreatedAt)
                .ToList();

            if (messagesToCompact.Count > 0)
            {
                await CompactConversationContextAsync(userId, sessionId, messagesToCompact);
                dbMessages = await _chatRepository.GetMessagesAsync(userId, sessionId, CompactionThreshold + 5);
                context = BuildContext(dbMessages);
            }
        }

        _cache.Set(key, context, _hotCacheExpiration);
        return context;
    }

    private async Task CompactConversationContextAsync(string userId, string sessionId, List<ChatMessageEntity> messages)
    {
        OnSseEvent?.Invoke("tool", new { name = "Compaction", state = "start", summary = $"Compacting {messages.Count} messages into context memory..." });

        var conversationText = new StringBuilder();
        foreach (var msg in messages)
        {
            var sanitized = SanitizeMessageForContext(msg.Role, msg.Message, MaxContextMessageLength);
            if (string.IsNullOrWhiteSpace(sanitized))
                continue;

            conversationText.AppendLine($"[{msg.Role}]: {sanitized}");
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

        // Keep full user-visible chat intact; rotate only summary snapshots used by AI context.
        var existingMessages = await _chatRepository.GetMessagesAsync(userId, sessionId, 200);
        var summaryIds = existingMessages
            .Where(m => m.Role == "summary")
            .Select(m => m.Id)
            .ToList();

        if (summaryIds.Count > 0)
        {
            await _chatRepository.DeleteMessagesAsync(userId, sessionId, summaryIds);
        }

        await _chatRepository.AddMessageAsync(summaryEntity);

        OnSseEvent?.Invoke("tool", new { name = "Compaction", state = "end", summary = "Conversation context compacted." });
    }

    public Task ClearConversationContextAsync(string userId, string sessionId)
    {
        var key = GetCacheKey(userId, sessionId);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<int> GetMessageCountAsync(string userId, string sessionId)
    {
        var messages = await _chatRepository.GetMessagesAsync(userId, sessionId, 100);
        return messages.Count(m => m.Role != "summary" && m.Role != "title" && m.Role != "state");
    }

    private static string? SanitizeMessageForContext(string? role, string? message, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var text = message.Trim();

        // Strip internal roadmap system payloads from context to avoid prompt bloat.
        if (role == "assistant" && text.StartsWith("[SYSTEM]", StringComparison.OrdinalIgnoreCase))
        {
            return "Roadmap generated successfully. Detailed payload is stored separately.";
        }

        if (text.Length > maxLength)
        {
            return text[..maxLength] + " ...[truncated]";
        }

        return text;
    }

    private static string GetCacheKey(string userId, string sessionId) => $"chat_{userId}_{sessionId}";

    private string BuildContext(List<ChatMessageEntity> dbMessages)
    {
        var sb = new StringBuilder();

        var compactContext = dbMessages.FirstOrDefault(m => m.Role == "summary");
        if (compactContext != null)
        {
            sb.AppendLine($"[Previous Compacted Context]: {compactContext.Message}");
        }

        var recentMessages = dbMessages.Where(m => m.Role != "summary" && m.Role != "title" && m.Role != "state")
            .OrderBy(m => m.CreatedAt)
            .TakeLast(5)
            .ToList();

        if (recentMessages.Any())
        {
            sb.AppendLine("Recent Conversation:");
            foreach (var msg in recentMessages)
            {
                var sanitized = SanitizeMessageForContext(msg.Role, msg.Message, MaxContextMessageLength);
                if (string.IsNullOrWhiteSpace(sanitized))
                    continue;

                sb.AppendLine($"[{msg.Role}]: {sanitized}");
            }
        }

        return sb.ToString();
    }
}
