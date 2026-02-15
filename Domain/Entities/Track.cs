namespace Domain.Entities;

public sealed class Track
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public string FixedTagsPayload { get; set; } = "";

    public int DomainId { get; set; }
    public Domains Domains { get; set; } = null!;

    public ICollection<TrackSection>? Sections { get; set; } = [];
}