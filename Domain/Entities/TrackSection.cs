namespace Domain.Entities;

public sealed class TrackSection
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public bool IsMultiSelect { get; set; }
    public int OrderIndex { get; set; }

    // Foreign Key: Link to Track
    public int TrackId { get; set; }
    public Track Track { get; set; } = null!;

    // Navigation: One Section -> Many Choices
    public ICollection<TrackChoice>? Choices { get; set; } = [];
}