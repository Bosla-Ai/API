using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs.AdministrationDTOs;

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

    [HttpPost("/AddDomain")]
    public async Task<ActionResult<APIResponse>> AddDomain([FromBody] DomainsDTO domainsDto)
    {
        var response = await serviceManager
            .Administration.AddDomain(domainsDto);
        return response;
    }
}