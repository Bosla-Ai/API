namespace Domain.Entities;

public class TrackTechnology
{
    public int TrackId { get; set; }
    public Track Track { get; set; }

    public int TechnologyId { get; set; }
    public Technology Technology { get; set; }

    public bool IsPrimary { get; set; } = false;
}