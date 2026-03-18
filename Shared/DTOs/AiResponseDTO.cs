using Shared.Enums;

namespace Shared.DTOs;

public class AiIntentDetectionResponse
{
    public string? Answer { get; set; }
    public LLMInteractionType InteractionType { get; set; }
    public float Confidence { get; set; } // 0-100%
    public bool Success { get; set; }
    public bool Thinking { get; set; }
    public string? ThinkingLog { get; set; }
    public string? ErrorMessage { get; set; }
    public string? VideoUrl { get; set; }
    public AskUserQuestion[]? Questions { get; set; }
}
