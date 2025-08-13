namespace Domain.Entities;

public sealed class LLMInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Prompt { get; set; }
    public string Response { get; set; }
}
