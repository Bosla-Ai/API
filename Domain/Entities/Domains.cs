namespace Domain.Entities;

public sealed class Domains
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    // One Domain has Many Tracks
    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}