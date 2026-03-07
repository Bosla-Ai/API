using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared.DTOs.DashboardDTOs;
using Shared.Options;
using StaticData = Shared.StaticData;

namespace Presentation.Controllers;


[EnableRateLimiting("AdminPolicy")]
public class DashboardController(
    IOptions<CookieSettingsOptions> cookieOptions
    , IServiceManager serviceManager) : ApiController(cookieOptions)
{
    [HttpGet]
    [Authorize(Roles = StaticData.SuperAdminRoleName)]
    public async Task<ActionResult<APIResponse<Dashboard>>> GetDashboardData()
    {
        var response = await serviceManager
            .Dashboard.GetDashboardDataAsync();
        return Ok(response);
    }
}
