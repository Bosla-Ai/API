using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs.DashboardDTOs;
using Domain.Responses;

namespace Presentation.Controllers;

public class DashboardController(
    IConfiguration configuration
    , IServiceManager serviceManager) : ApiController(configuration)
{
    [HttpGet]
    public async Task<ActionResult<APIResponse<Dashboard>>> GetDashboardData()
    {
        var response = await serviceManager
            .Dashboard.GetDashboardDataAsync();
        return Ok(response);
    }
}