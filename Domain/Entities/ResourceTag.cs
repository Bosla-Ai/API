namespace Domain.Entities;

public class ResourceTag
{
    public string ResourceId { get; set; } = Guid.NewGuid().ToString();
    public Resource Resource { get; set; }
    public string Tag { get; set; }
    public double Relevance { get; set; } = 1.0; // How relevant is this tag (for ranking)
}

