namespace Shared.DTOs.AdministrationDTOs;

public sealed class DomainsDTO
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public ICollection<TrackDTO> Tracks { get; set; } = new List<TrackDTO>();
}