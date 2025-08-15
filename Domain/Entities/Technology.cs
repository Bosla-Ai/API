using Shared.Enums;

namespace Domain.Entities;

public sealed class Technology
{
    public int Id { get; set; }
    public string Name { get; set; }
    public TechnologyType Type { get; set; }
    public string Category { get; set; }

    public ICollection<TrackTechnology> TrackTechnologies { get; set; } = new List<TrackTechnology>();
    public ICollection<TopicTechnology> TopicTechnologies { get; set; } = new List<TopicTechnology>();
}