using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;

namespace Presintation.Controllers;

// [Authorize(Roles = StaticData.AdminRoleName)]
public class AdministrationController(
    IConfiguration configuration
    , IServiceManager serviceManager) : ApiController(configuration)
{
    [HttpGet("/GetDomains")]
    public async Task<ActionResult<APIResponse>> GetDomains([FromQuery] bool isActive = true)
    {
        var domains = await serviceManager
            .Administration.GetDomainsAsync(isActive);
        return domains;
    }
}