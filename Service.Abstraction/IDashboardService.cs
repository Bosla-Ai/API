using Domain.Responses;
using Shared.DTOs.DashboardDTOs;

namespace Service.Abstraction;

public interface IDashboardService
{
    Task<APIResponse<Dashboard>> GetDashboardDataAsync();
}
