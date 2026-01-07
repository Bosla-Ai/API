using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs.AdministrationDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;

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

    [HttpGet("GetDomain/{id:int}")]
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

    [HttpDelete("DeleteDomain/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteDomain(int id)
    {
        var response = await serviceManager
            .Administration.DeleteDomain(id);
        return response;
    }

    [HttpGet("GetTracks/{domainId:int}")]
    public async Task<ActionResult<APIResponse>> GetTracks(int domainId)
    {
        var response = await serviceManager
            .Administration.GetTracks(domainId);
        return response;
    }

    [HttpGet("GetTrack/{id:int}")]
    public async Task<ActionResult<APIResponse>> GetTrack(int id)
    {
        var response = await serviceManager
            .Administration.GetTrack(id);
        return response;
    }

    [HttpPost("AddTrack")]
    public async Task<ActionResult<APIResponse>> AddTrack([FromBody] TrackCreateDTO trackCreateDto)
    {
        var response = await serviceManager
            .Administration.AddTrack(trackCreateDto);
        return response;
    }

    [HttpPut("UpdateTrack")]
    public async Task<ActionResult<APIResponse>> UpdateTrack([FromBody] TrackUpdateDTO trackUpdateDto)
    {
        var response = await serviceManager
            .Administration.UpdateTrack(trackUpdateDto);
        return response;
    }

    [HttpDelete("DeleteTrack/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteTrack(int id)
    {
        var response = await serviceManager
            .Administration.DeleteTrack(id);
        return response;
    }
}