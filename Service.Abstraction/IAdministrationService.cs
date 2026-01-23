using Domain.Entities;
using Domain.Responses;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace Service.Abstraction;

public interface IAdministrationService
{
    Task<APIResponse<IEnumerable<DomainsDTO>>> GetDomainsAsync(bool isActive);
    Task<APIResponse<DomainsDTO>> GetDomainAsync(int id);
    Task<APIResponse<int>> AddDomain(DomainCreateDTO domainsDto);
    Task<APIResponse> UpdateDomain(DomainUpdateDTO domainsDto);
    Task<APIResponse> DeleteDomain(int id);

    Task<APIResponse<IEnumerable<TrackDTO>>> GetTracks(int domainId);
    Task<APIResponse<TrackDTO>> GetTrack(int id);
    Task<APIResponse<int>> AddTrack(TrackCreateDTO trackDto);
    Task<APIResponse> UpdateTrack(TrackUpdateDTO trackDto);
    Task<APIResponse> DeleteTrack(int id);

    Task<APIResponse<IEnumerable<TrackSectionDTO>>> GetTrackSections(int trackId);
    Task<APIResponse<TrackSectionDTO>> GetTrackSection(int id);
    Task<APIResponse<int>> AddTrackSection(TrackSectionCreateDTO trackSectionDto);
    Task<APIResponse> UpdateTrackSection(TrackSectionUpdateDTO trackSectionDto);
    Task<APIResponse> DeleteTrackSection(int id);

    Task<APIResponse<IEnumerable<TrackChoiceDTO>>> GetTrackChoices(int trackId);
    Task<APIResponse<TrackChoiceDTO>> GetTrackChoice(int id);
    Task<APIResponse<int>> AddTrackChoice(TrackChoiceCreateDTO trackChoiceDto);
    Task<APIResponse> UpdateTrackChoice(TrackChoiceUpdateDTO trackChoiceDto);
    Task<APIResponse> DeleteTrackChoice(int id);
}