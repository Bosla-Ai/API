using System.ComponentModel.DataAnnotations;

namespace Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;

public sealed class TrackChoiceDTO
{
    public int Id { get; set; }
    public int SectionId { get; set; }

    public string Title { get; set; } = "";
    public string TagsPayload { get; set; } = "";

    public bool IsDefault { get; set; } = false;
}