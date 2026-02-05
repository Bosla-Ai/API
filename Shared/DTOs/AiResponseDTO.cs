using Shared.Enums;

namespace Shared.DTOs;

public class AiIntentDetectionResponse
{
    public string? Answer { get; set; }
    public LLMInteractionType InteractionType { get; set; }
    public float Confidence { get; set; } // 0-100%
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}