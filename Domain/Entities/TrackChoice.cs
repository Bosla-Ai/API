namespace Domain.Entities;

public sealed class TrackChoice
{
    public int Id { get; set; }
    
    public string Label { get; set; } = "";       
    
    public string TagsPayload { get; set; } = ""; 

    public bool IsDefault { get; set; } = false;

    public int SectionId { get; set; }
    public TrackSection Section { get; set; } = null!;
}