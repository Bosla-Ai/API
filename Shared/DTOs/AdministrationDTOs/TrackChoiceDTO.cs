namespace Shared.DTOs.AdministrationDTOs;

public sealed class TrackChoiceDTO
{
    public string Label { get; set; } = "";
    public string TagsPayload { get; set; } = "";
    public bool IsDefault { get; set; } = false;
}