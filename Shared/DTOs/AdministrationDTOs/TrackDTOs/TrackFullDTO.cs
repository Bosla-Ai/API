using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace Shared.DTOs.AdministrationDTOs.TrackDTOs;

public sealed class TrackFullDTO : TrackDTO
{
    public ICollection<TrackSectionFullDTO> Sections { get; set; } = new List<TrackSectionFullDTO>();
}
