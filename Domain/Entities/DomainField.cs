namespace Domain.Entities;

public sealed class Domain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
    public string Slug { get; set; }
    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}
