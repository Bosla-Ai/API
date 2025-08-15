namespace Domain.Entities;

public class TopicTechnology
{
    public int TopicId { get; set; }
    public Topic Topic { get; set; }

    public int TechnologyId { get; set; }
    public Technology Technology { get; set; }

    public bool IsRecommended { get; set; } = true; 
}