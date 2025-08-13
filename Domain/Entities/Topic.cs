using Shared.Enums;

namespace Domain.Entities;

public sealed class Topic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; }
    public string Slug { get; set; }
    public string Description { get; set; }

    public LevelType Level { get; set; } = LevelType.Beginner;
    public int RecommendedHours { get; set; }

    public Guid? TrackId { get; set; }
    public Track Track { get; set; }

    public ICollection<TopicTechnology> TopicTechnologies { get; set; } = new List<TopicTechnology>();
}
