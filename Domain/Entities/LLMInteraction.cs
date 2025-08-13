using Shared.Enums;

namespace Domain.Entities;

public class LLMInteraction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    
    public LLMInteractionType Type { get; set; }
    public string InputData { get; set; } = "{}"; // What was sent to LLM
    public string OutputData { get; set; } = "{}"; // What LLM returned
    public string ModelUsed { get; set; } = "gpt-3.5-turbo";
    public int TokensUsed { get; set; } = 0;
    public bool WasSuccessful { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Optional References
    public Guid? RoadMapId { get; set; }
    public RoadMap? RoadMap { get; set; }
}