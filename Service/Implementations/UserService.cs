using System.Net;
using Domain.Contracts;
using Domain.Responses;
using Service.Abstraction;
using Shared.DTOs.DashboardDTOs;

namespace Service.Implementations;

public class UserService(IUnitOfWork unitOfWork) : IUserService
{
    public async Task<APIResponse<IEnumerable<DashboardDomainDTO>>> GetAllDomainsWithHierarchyAsync(bool? isActive = null)
    {
        var flatResults = await unitOfWork.GetDomainsHierarchyAsync(isActive);

        var domains = TransformToHierarchy(flatResults);

        return new APIResponse<IEnumerable<DashboardDomainDTO>>
        {
            StatusCode = HttpStatusCode.OK,
            Data = domains
        };
    }

    private static IEnumerable<DashboardDomainDTO> TransformToHierarchy(List<DashboardFlatResult> flatResults)
    {
        var domains = new Dictionary<int, DashboardDomainDTO>();

        foreach (var row in flatResults)
        {
            if (!domains.TryGetValue(row.DomainId, out var domain))
            {
                domain = new DashboardDomainDTO
                {
                    Id = row.DomainId,
                    Title = row.DomainTitle,
                    Description = row.DomainDescription,
                    IconUrl = row.DomainIconUrl,
                    IsActive = row.DomainIsActive,
                    Tracks = new List<DashboardTrackDTO>()
                };
                domains[row.DomainId] = domain;
            }

            if (!row.TrackId.HasValue)
                continue;

            var track = domain.Tracks.FirstOrDefault(t => t.Id == row.TrackId);
            if (track == null)
            {
                track = new DashboardTrackDTO
                {
                    Id = row.TrackId.Value,
                    Title = row.TrackTitle ?? "",
                    Description = row.TrackDescription ?? "",
                    IconUrl = row.TrackIconUrl ?? "",
                    IsActive = row.TrackIsActive ?? true,
                    FixedTagsPayload = row.FixedTagsPayload ?? "",
                    Sections = new List<DashboardTrackSectionDTO>()
                };
                domain.Tracks.Add(track);
            }

            if (!row.SectionId.HasValue)
                continue;

            var section = track.Sections.FirstOrDefault(s => s.Id == row.SectionId);
            if (section == null)
            {
                section = new DashboardTrackSectionDTO
                {
                    Id = row.SectionId.Value,
                    Title = row.SectionTitle ?? "",
                    IsMultiSelect = row.IsMultiSelect ?? false,
                    OrderIndex = row.OrderIndex ?? 0,
                    Choices = new List<DashboardTrackChoiceDTO>()
                };
                track.Sections.Add(section);
            }

            if (!row.ChoiceId.HasValue)
                continue;

            if (!section.Choices.Any(c => c.Id == row.ChoiceId))
            {
                section.Choices.Add(new DashboardTrackChoiceDTO
                {
                    Id = row.ChoiceId.Value,
                    Label = row.ChoiceLabel ?? "",
                    TagsPayload = row.ChoiceTagsPayload ?? "",
                    IsDefault = row.ChoiceIsDefault ?? false
                });
            }
        }

        return domains.Values;
    }
}
