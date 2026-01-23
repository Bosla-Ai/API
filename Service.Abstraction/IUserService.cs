using Domain.Responses;
using Shared.DTOs.DashboardDTOs;

namespace Service.Abstraction;

public interface IUserService
{
    Task<APIResponse<IEnumerable<DashboardDomainDTO>>> GetAllDomainsWithHierarchyAsync(bool? isActive = null);
}
