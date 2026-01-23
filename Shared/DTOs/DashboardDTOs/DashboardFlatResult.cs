using Microsoft.EntityFrameworkCore;

namespace Shared.DTOs.DashboardDTOs;

[Keyless]
public sealed class DashboardFlatResult
{
    // Domain columns
    public int DomainId { get; set; }
    public string DomainTitle { get; set; } = "";
    public string DomainDescription { get; set; } = "";
    public string DomainIconUrl { get; set; } = "";
    public bool DomainIsActive { get; set; }

    // Track columns
    public int? TrackId { get; set; }
    public string? TrackTitle { get; set; }
    public string? TrackDescription { get; set; }
    public string? TrackIconUrl { get; set; }
    public bool? TrackIsActive { get; set; }
    public string? FixedTagsPayload { get; set; }

    // Section columns
    public int? SectionId { get; set; }
    public string? SectionTitle { get; set; }
    public bool? IsMultiSelect { get; set; }
    public int? OrderIndex { get; set; }

    // Choice columns
    public int? ChoiceId { get; set; }
    public string? ChoiceLabel { get; set; }
    public string? ChoiceTagsPayload { get; set; }
    public bool? ChoiceIsDefault { get; set; }
}
