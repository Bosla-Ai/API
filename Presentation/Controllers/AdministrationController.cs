using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;

namespace Presentation.Controllers;

// [Authorize(Roles = StaticData.AdminRoleName)]
public class AdministrationController(
    IConfiguration configuration
    , IServiceManager serviceManager) : ApiController(configuration)
{
    [HttpGet("GetDomains")]
    public async Task<ActionResult<APIResponse>> GetDomains([FromQuery] bool isActive = true)
    {
        var domains = await serviceManager
            .Administration.GetDomainsAsync(isActive);
        return domains;
    }

    [HttpGet("GetDomain/{id}")]
    public async Task<ActionResult<APIResponse>> GetDomain(int id)
    {
        var response = await serviceManager
            .Administration.GetDomainAsync(id);
        return response;
    }

    [HttpPost("AddDomain")]
    public async Task<ActionResult<APIResponse>> AddDomain([FromBody] DomainCreateDTO domainsDto)
    {
        var response = await serviceManager
            .Administration.AddDomain(domainsDto);
        return response;
    }

    [HttpPut("UpdateDomain")]
    public async Task<ActionResult<APIResponse>> UpdateDomain([FromBody] DomainUpdateDTO domainsDto)
    {
        var response = await serviceManager
            .Administration.UpdateDomain(domainsDto);
        return response;
    }

    [HttpDelete("DeleteDomain/{id}")]
    public async Task<ActionResult<APIResponse>> DeleteDomain(int id)
    {
        var response = await serviceManager
            .Administration.DeleteDomain(id);
        return response;
    }
}