using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackChoiceDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.DTOs.AdministrationDTOs.TrackSectionDTOs;

namespace Presentation.Controllers;

[Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
public class AdministrationController(
    IConfiguration configuration,
    IServiceManager serviceManager) : ApiController(configuration)
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
            .Administration.GetFullTrack(id);
        return response;
    }

    [HttpPost("AddTrack")]
    public async Task<ActionResult<APIResponse>> AddTrack([FromBody] TrackCreateFullDTO trackCreateDto)
    {
        var response = await serviceManager
            .Administration.AddTrackFull(trackCreateDto);
        return response;
    }

    [HttpPut("UpdateTrack")]
    public async Task<ActionResult<APIResponse>> UpdateTrack([FromBody] TrackUpdateFullDTO trackUpdateDto)
    {
        var response = await serviceManager
            .Administration.UpdateFullTrack(trackUpdateDto);
        return response;
    }

    [HttpDelete("DeleteTrack/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteTrack(int id)
    {
        var response = await serviceManager
            .Administration.DeleteTrack(id);
        return response;
    }

    [HttpDelete("DeleteSection/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteSection(int id)
    {
        var response = await serviceManager
            .Administration.DeleteSection(id);
        return response;
    }

    [HttpDelete("DeleteChoice/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteChoice(int id)
    {
        var response = await serviceManager
            .Administration.DeleteChoice(id);
        return response;
    }
}