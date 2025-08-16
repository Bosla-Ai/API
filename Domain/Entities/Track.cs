namespace Domain.Entities;

public sealed class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }

    public Guid DomainId { get; set; }
    public DomainField DomainField { get; set; }

    public ICollection<TrackTechnology> TrackTechnologies { get; set; } = new List<TrackTechnology>();
    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
}
