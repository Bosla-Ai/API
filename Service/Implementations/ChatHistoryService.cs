using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        if (string.IsNullOrEmpty(userId))
            throw new BadRequestException("Invalid user ID");

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
        if (string.IsNullOrEmpty(userId))
            throw new BadRequestException("Invalid user ID");

        var allMessages = await chatRepository.GetAllUserMessagesAsync(userId);

        if (allMessages == null || allMessages.Count == 0)
        {
            return new APIResponse<List<ChatSessionSummaryDTO>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = []
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

                // Preview from latest assistant message, skipping [SYSTEM] and [CANCELLED] prefixed ones
                var previewMessage = messages
                    .FirstOrDefault(m => m.Role == "assistant" && !m.Message.StartsWith("[SYSTEM]") && m.Message != "[CANCELLED]")
                    ?? latestMessage;

                // Prefer AI-generated title if persisted, else fall back to first user message
                var titleMessage = messages.FirstOrDefault(m => m.Role == "title");
                var title = titleMessage?.Message
                    ?? TruncatePreview(StripModePrefix(firstUserMessage?.Message ?? "New Chat"));

                return new ChatSessionSummaryDTO
                {
                    SessionId = group.Key,
                    Title = title,
                    LastMessageAt = latestMessage.CreatedAt,
                    LastMessagePreview = TruncatePreview(previewMessage.Message),
                    MessageCount = messages.Count(m => m.Role != "title" && m.Role != "summary" && m.Role != "state")
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
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sessionId))
            throw new BadRequestException("Invalid user ID or session ID");

        var messages = await chatRepository.GetMessagesAsync(userId, sessionId, limit: 100);

        if (messages == null || messages.Count == 0)
            throw new NotFoundException("Chat session not found");

        // Renew cleanup timer when user views a session
        _ = chatRepository.TouchSessionAsync(userId, sessionId);

        return new APIResponse<ChatSessionMessagesDTO>
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ChatSessionMessagesDTO
            {
                SessionId = sessionId,
                Messages = [.. messages
                    .Where(m => m.Role != "title" && m.Role != "summary" && m.Role != "state")
                    .Select(m => new ChatMessageDTO
                    {
                        Role = m.Role,
                        Message = m.Message,
                        CreatedAt = m.CreatedAt
                    })]
            }
        };
    }

    public async Task<APIResponse<int>> DeleteSessionAsync(string userId, string sessionId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sessionId))
            throw new BadRequestException("Invalid user ID or session ID");

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
        if (maxAgeDays <= 0 || renewalGraceDays <= 0)
            throw new BadRequestException("Invalid max age or renewal grace days");

        var createdCutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        var accessCutoff = DateTime.UtcNow.AddDays(-renewalGraceDays);
        var deletedCount = await chatRepository.DeleteInactiveMessagesAsync(createdCutoff, accessCutoff);

        logger.LogInformation(
            "Chat cleanup completed: deleted {DeletedCount} messages (older than {MaxAge}d, not accessed in {Grace}d)",
            deletedCount, maxAgeDays, renewalGraceDays);

        return deletedCount;
    }

    public async Task<APIResponse> CancelRequestAsync(string userId, string sessionId, string? partialResponse = null)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sessionId))
            throw new BadRequestException("Invalid user ID or session ID");

        // If there was partial streamed text before cancellation, save it first
        if (!string.IsNullOrEmpty(partialResponse))
        {
            await chatRepository.AddMessageAsync(new ChatMessageEntity
            {
                UserId = userId,
                SessionId = sessionId,
                Message = partialResponse,
                Role = "assistant",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Save the cancellation marker
        await chatRepository.AddMessageAsync(new ChatMessageEntity
        {
            UserId = userId,
            SessionId = sessionId,
            Message = "[CANCELLED]",
            Role = "assistant",
            CreatedAt = DateTime.UtcNow
        });

        logger.LogInformation(
            "Chat request cancelled for session {SessionId}, user {UserId}. Partial text: {HasPartial}",
            sessionId, userId, !string.IsNullOrEmpty(partialResponse));

        return new APIResponse
        {
            StatusCode = HttpStatusCode.OK
        };
    }

    private static string TruncatePreview(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        if (message.Length <= MESSAGE_PREVIEW_LENGTH)
            return message;

        return message[..MESSAGE_PREVIEW_LENGTH] + "...";
    }

    private static string StripModePrefix(string message)
    {
        var stripped = Regex.Replace(message, @"^\[Active Mode: [^\]]+\]\n?", "").Trim();
        return string.IsNullOrEmpty(stripped) ? message : stripped;
    }
}
