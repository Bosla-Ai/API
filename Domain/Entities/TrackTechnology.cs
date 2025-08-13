namespace Domain.Entities;

public sealed class TrackTechnology
{
    public Guid TrackId { get; set; }
    public Track Track { get; set; }

    public Guid TechnologyId { get; set; }
    public Technology Technology { get; set; }

    public bool IsPrimary { get; set; } = false;
}
