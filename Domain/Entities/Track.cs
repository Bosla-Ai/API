namespace Domain.Entities;

public sealed class Track : BaseEntity
{
    public string Name { get; set; }

    public int DomainId { get; set; }
    public Domain Domain { get; set; }

    public ICollection<TrackTechnology> TrackTechnologies { get; set; } = new List<TrackTechnology>();
    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
}