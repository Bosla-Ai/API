namespace Shared.DTOs;

public class ChatSessionSummaryDTO
{
    public string SessionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public int MessageCount { get; set; }
}

public class ChatMessageDTO
{
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatSessionMessagesDTO
{
    public string SessionId { get; set; } = string.Empty;
    public List<ChatMessageDTO> Messages { get; set; } = [];
}
