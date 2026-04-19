using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.DTOs;

public class SubmitFeedbackRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    public string? MessageId { get; set; }

    [Required]
    [RegularExpression("^(up|down)$", ErrorMessage = "Rating must be 'up' or 'down'")]
    public string Rating { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public LLMInteractionType? IntentType { get; set; }
}
