using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Helpers;

public class ConversationContextManager
{
    private readonly IMemoryCache _cache;
    private readonly IChatRepository _chatRepository;
    private readonly CustomerHelper _customerHelper;
    private readonly TimeSpan _hotCacheExpiration = TimeSpan.FromMinutes(5);
    private const int SummarizationThreshold = 10;

    public ConversationContextManager(IMemoryCache cache, IChatRepository chatRepository, CustomerHelper customerHelper)
    {
        _cache = cache;
        _chatRepository = chatRepository;
        _customerHelper = customerHelper;
    }

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
            .GetMessagesAsync(userId, sessionId, SummarizationThreshold + 5);

        if (!dbMessages.Any())
        {
            return string.Empty;
        }

        var regularMessages = dbMessages.Where(m => m.Role != "summary").ToList();
        var existingSummary = dbMessages.FirstOrDefault(m => m.Role == "summary");

        if (regularMessages.Count >= SummarizationThreshold && existingSummary == null)
        {
            await SummarizeAndCompressAsync(userId, sessionId, regularMessages);
            dbMessages = await _chatRepository.GetMessagesAsync(userId, sessionId, SummarizationThreshold + 5);
        }

        var sb = new StringBuilder();

        var summary = dbMessages.FirstOrDefault(m => m.Role == "summary");
        if (summary != null)
        {
            sb.AppendLine($"[Previous Context Summary]: {summary.Message}");
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

    private async Task SummarizeAndCompressAsync(string userId, string sessionId, List<ChatMessageEntity> messages)
    {
        var conversationText = new StringBuilder();
        foreach (var msg in messages)
        {
            conversationText.AppendLine($"[{msg.Role}]: {msg.Message}");
        }

        var summary = await _customerHelper.SummarizeConversationAsync(conversationText.ToString());

        var messageIds = messages.Select(m => m.Id).ToList();
        await _chatRepository.DeleteMessagesAsync(userId, sessionId, messageIds);

        var summaryEntity = new ChatMessageEntity
        {
            UserId = userId,
            SessionId = sessionId,
            Message = summary,
            Role = "summary",
            CreatedAt = DateTime.UtcNow
        };
        await _chatRepository.AddMessageAsync(summaryEntity);
    }

    public Task ClearConversationContextAsync(string userId, string sessionId)
    {
        var key = GetCacheKey(userId, sessionId);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    private static string GetCacheKey(string userId, string sessionId) => $"chat_{userId}_{sessionId}";
}