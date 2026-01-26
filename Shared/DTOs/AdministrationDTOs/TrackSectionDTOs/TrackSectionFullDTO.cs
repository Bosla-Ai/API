using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;

namespace Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

public sealed class TrackSectionFullDTO : TrackSectionDTO
{
    public ICollection<TrackChoiceDTO> Choices { get; set; } = new List<TrackChoiceDTO>();
}
