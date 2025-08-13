namespace Domain.Entities;

public sealed class TopicTechnology
{
    public Guid TopicId { get; set; }
    public Topic Topic { get; set; }

    public Guid TechnologyId { get; set; }
    public Technology Technology { get; set; }

    public bool IsRecommended { get; set; } = true; 
}
