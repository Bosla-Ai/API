using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared.DTOs.DashboardDTOs;
using Domain.Responses;
using Shared.Options;

namespace Presentation.Controllers;


public class DashboardController(
    IOptions<CookieSettingsOptions> cookieOptions
    , IServiceManager serviceManager) : ApiController(cookieOptions)
{
    [HttpGet]
    public async Task<ActionResult<APIResponse<Dashboard>>> GetDashboardData()
    {
        var response = await serviceManager
            .Dashboard.GetDashboardDataAsync();
        return Ok(response);
    }
}