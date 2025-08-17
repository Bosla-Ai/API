namespace Domain.Entities;

public class TopicTechnology : BaseManyToManyEntity<Topic, int, Technology, int>
{
    public int TopicId 
    { 
        get => LeftId; 
        set => LeftId = value; 
    }
    
    public Topic Topic 
    { 
        get => Left; 
        set => Left = value; 
    }

    public int TechnologyId 
    { 
        get => RightId; 
        set => RightId = value; 
    }
    
    public Technology Technology 
    { 
        get => Right; 
        set => Right = value; 
    }

    public bool IsRecommended { get; set; } = true; 
}