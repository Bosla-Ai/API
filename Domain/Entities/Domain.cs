namespace Domain.Entities;

public sealed class Domain : BaseEntity
{
    public string Name { get; set; }
    public string Slug { get; set; }
    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}