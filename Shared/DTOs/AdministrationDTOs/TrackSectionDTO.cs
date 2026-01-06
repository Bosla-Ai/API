namespace Shared.DTOs.AdministrationDTOs;

public sealed class TrackSectionDTO
{
    public string Title { get; set; } = "";
    public bool IsMultiSelect { get; set; }
    public int OrderIndex { get; set; }

    public ICollection<TrackChoiceDTO> Choices { get; set; } = new List<TrackChoiceDTO>();
}