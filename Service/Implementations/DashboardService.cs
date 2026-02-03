using System.Net;
using Domain.Contracts;
using Domain.Entities;
using Domain.ModelsSpecifications;
using Domain.Responses;
using Service.Abstraction;
using Shared.DTOs.DashboardDTOs;

namespace Service.Implementations;

public class DashboardService(IUnitOfWork unitOfWork) : IDashboardService
{
    public async Task<APIResponse<Dashboard>> GetDashboardDataAsync()
    {
        var roadmapsCount = await unitOfWork.GetRepo<Roadmap, int>().CountAsync();
        var customersCount = await unitOfWork.GetRepo<Customer, int>().CountAsync();
        var coursesCount = await unitOfWork.GetRepo<Course, int>().CountAsync();
        var domainsCount = await unitOfWork.GetRepo<Domains, int>().CountAsync();

        // Online users logic: Count unique UserIds with valid refresh tokens
        var onlineUsersCount = await unitOfWork.GetRepo<RefreshToken, int>()
            .CountDistinctAsync(new ActiveRefreshTokensSpecification(), r => r.UserId);

        var dashboardData = new Dashboard
        {
            RoadmapsGenerartedCount = roadmapsCount,
            AllCustomersCount = customersCount,
            OnlineUsersCount = onlineUsersCount,
            CoursesStoredCount = coursesCount,
            DomainsCount = domainsCount
        };

        return new APIResponse<Dashboard>
        {
            StatusCode = HttpStatusCode.OK,
            Data = dashboardData
        };
    }
}
