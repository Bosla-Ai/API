using Domain.Responses;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;

namespace Service.Abstraction;

public interface IAdministrationService
{
    Task<APIResponse<IEnumerable<DomainsDTO>>> GetDomainsAsync(bool isActive);
    Task<APIResponse<DomainsDTO>> GetDomainAsync(int id);
    Task<APIResponse> AddDomain(DomainCreateDTO domainsDto);
    Task<APIResponse> UpdateDomain(DomainUpdateDTO domainsDto);
    Task<APIResponse> DeleteDomain(int id);
}