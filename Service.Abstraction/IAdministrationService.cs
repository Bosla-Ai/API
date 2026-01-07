using Domain.Responses;
using Shared.DTOs.AdministrationDTOs;

namespace Service.Abstraction;

public interface IAdministrationService
{
    Task<APIResponse<IEnumerable<DomainsDTO>>> GetDomainsAsync(bool isActive);
    Task<APIResponse> AddDomain(DomainsDTO domainsDto);
}