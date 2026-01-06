namespace Shared.DTOs.AdministrationDTOs;

public sealed class TrackDTO
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public string FixedTagsPayload { get; set; } = "";
    public ICollection<TrackSectionDTO> Sections { get; set; } = new List<TrackSectionDTO>();
}