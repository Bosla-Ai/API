namespace Domain.Entities;

public class TrackTechnology : BaseManyToManyEntity<Track, int, Technology, int>
{
    public int TrackId 
    { 
        get => LeftId; 
        set => LeftId = value; 
    }
    
    public Track Track 
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

    public bool IsPrimary { get; set; } = false;
}