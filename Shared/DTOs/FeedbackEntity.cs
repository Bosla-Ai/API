using Newtonsoft.Json;
using Shared.Enums;

namespace Shared.DTOs;

public class FeedbackEntity
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public string? MessageId { get; set; }

    public string Rating { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public string? Reason { get; set; }

    public LLMInteractionType? IntentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
