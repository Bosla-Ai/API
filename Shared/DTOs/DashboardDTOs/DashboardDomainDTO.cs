namespace Shared.DTOs.DashboardDTOs;

public sealed class DashboardDomainDTO
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; }
    public List<DashboardTrackDTO> Tracks { get; set; } = new();
}

public sealed class DashboardTrackDTO
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public bool IsActive { get; set; }
    public string FixedTagsPayload { get; set; } = "";
    public List<DashboardTrackSectionDTO> Sections { get; set; } = new();
}

public sealed class DashboardTrackSectionDTO
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsMultiSelect { get; set; }
    public int OrderIndex { get; set; }
    public List<DashboardTrackChoiceDTO> Choices { get; set; } = new();
}

public sealed class DashboardTrackChoiceDTO
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public string TagsPayload { get; set; } = "";
    public bool IsDefault { get; set; }
}
