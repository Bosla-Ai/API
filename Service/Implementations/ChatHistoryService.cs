using System.Net;
using System.Security.Cryptography;
using System.Text;
using Domain.Exceptions;
using Domain.Responses;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Implementations;

public class ChatHistoryService(
    IChatRepository chatRepository,
    ILogger<ChatHistoryService> logger) : IChatHistoryService
{
    private const int MESSAGE_PREVIEW_LENGTH = 80;

    public Task<APIResponse<string>> StartNewSessionAsync(string userId)
    {
        var input = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sessionId = Convert.ToHexString(hash)[..16].ToLower();

        return Task.FromResult(new APIResponse<string>
        {
            StatusCode = HttpStatusCode.OK,
            Data = sessionId
        });
    }

    public async Task<APIResponse<List<ChatSessionSummaryDTO>>> GetUserChatSessionsAsync(string userId)
    {
        var allMessages = await chatRepository.GetAllUserMessagesAsync(userId);

        if (allMessages.Count == 0)
        {
            return new APIResponse<List<ChatSessionSummaryDTO>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = new List<ChatSessionSummaryDTO>()
            };
        }

        var sessions = allMessages
            .GroupBy(m => m.SessionId)
            .Select(group =>
            {
                var messages = group.ToList();
                var latestMessage = messages.First();

                // Title from the first user message (oldest in desc-ordered list)
                var firstUserMessage = messages
                    .LastOrDefault(m => m.Role == "user");

                // Preview from latest assistant message, skipping [SYSTEM] prefixed ones
                var previewMessage = messages
                    .FirstOrDefault(m => m.Role == "assistant" && !m.Message.StartsWith("[SYSTEM]"))
                    ?? latestMessage;

                return new ChatSessionSummaryDTO
                {
                    SessionId = group.Key,
                    Title = TruncatePreview(firstUserMessage?.Message ?? "New Chat"),
                    LastMessageAt = latestMessage.CreatedAt,
                    LastMessagePreview = TruncatePreview(previewMessage.Message),
                    MessageCount = messages.Count
                };
            })
            .OrderByDescending(s => s.LastMessageAt)
            .ToList();

        return new APIResponse<List<ChatSessionSummaryDTO>>
        {
            StatusCode = HttpStatusCode.OK,
            Data = sessions
        };
    }

    public async Task<APIResponse<ChatSessionMessagesDTO>> GetSessionMessagesAsync(string userId, string sessionId)
    {
        var messages = await chatRepository.GetMessagesAsync(userId, sessionId, limit: 100);

        // Renew cleanup timer when user views a session
        _ = chatRepository.TouchSessionAsync(userId, sessionId);

        return new APIResponse<ChatSessionMessagesDTO>
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ChatSessionMessagesDTO
            {
                SessionId = sessionId,
                Messages = messages
                    .Select(m => new ChatMessageDTO
                    {
                        Role = m.Role,
                        Message = m.Message,
                        CreatedAt = m.CreatedAt
                    })
                    .ToList()
            }
        };
    }

    public async Task<APIResponse<int>> DeleteSessionAsync(string userId, string sessionId)
    {
        var deletedCount = await chatRepository.DeleteSessionAsync(userId, sessionId);

        if (deletedCount == 0)
            throw new NotFoundException("Chat session not found");

        logger.LogInformation(
            "Deleted chat session {SessionId} for user {UserId}: {Count} messages removed",
            sessionId, userId, deletedCount);

        return new APIResponse<int>
        {
            StatusCode = HttpStatusCode.OK,
            Data = deletedCount
        };
    }

    public async Task<int> CleanInactiveChatsAsync(int maxAgeDays, int renewalGraceDays)
    {
        var createdCutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        var accessCutoff = DateTime.UtcNow.AddDays(-renewalGraceDays);
        var deletedCount = await chatRepository.DeleteInactiveMessagesAsync(createdCutoff, accessCutoff);

        logger.LogInformation(
            "Chat cleanup completed: deleted {DeletedCount} messages (older than {MaxAge}d, not accessed in {Grace}d)",
            deletedCount, maxAgeDays, renewalGraceDays);

        return deletedCount;
    }

    private static string TruncatePreview(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        if (message.Length <= MESSAGE_PREVIEW_LENGTH)
            return message;

        return message[..MESSAGE_PREVIEW_LENGTH] + "...";
    }
}
