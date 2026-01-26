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
    Task<APIResponse<TrackFullDTO>> GetFullTrack(int id);
    Task<APIResponse<int>> AddTrackFull(TrackCreateFullDTO trackDto);
    Task<APIResponse> UpdateFullTrack(TrackUpdateFullDTO trackDto);
    Task<APIResponse> DeleteTrack(int id);
}