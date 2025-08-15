using Shared.Enums;

namespace Domain.Entities;

public class Topic
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Slug { get; set; }
    public string Description { get; set; }

    // optional primary language (e.g., Python)
    public int? PrimaryLanguageId { get; set; }
    public Technology PrimaryLanguage { get; set; }

    public LevelType Level { get; set; } = LevelType.Beginner;
    public int RecommendedHours { get; set; }

    public int? TrackId { get; set; }
    public Track Track { get; set; }

    public ICollection<TopicTechnology> TopicTechnologies { get; set; } = new List<TopicTechnology>();
}