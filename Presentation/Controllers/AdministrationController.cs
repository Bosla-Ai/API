using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared;
using Shared.DTOs.AdministrationDTOs.AdminDTOs;
using Shared.DTOs.AdministrationDTOs.DomainDTOs;
using Shared.DTOs.AdministrationDTOs.TrackDTOs;
using Shared.Options;

namespace Presentation.Controllers;


[EnableRateLimiting("AdminPolicy")]
public class AdministrationController(
    IOptions<CookieSettingsOptions> cookieOptions,
    IServiceManager serviceManager) : ApiController(cookieOptions)
{
    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpGet("GetDomains")]
    public async Task<ActionResult<APIResponse>> GetDomains([FromQuery] bool isActive = true)
    {
        var domains = await serviceManager
            .Administration.GetDomainsAsync(isActive);
        return domains;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpGet("GetDomain/{id:int}")]
    public async Task<ActionResult<APIResponse>> GetDomain(int id)
    {
        var response = await serviceManager
            .Administration.GetDomainAsync(id);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpPost("AddDomain")]
    public async Task<ActionResult<APIResponse>> AddDomain([FromBody] DomainCreateDTO domainsDto)
    {
        var response = await serviceManager
            .Administration.AddDomain(domainsDto);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpPut("UpdateDomain")]
    public async Task<ActionResult<APIResponse>> UpdateDomain([FromBody] DomainUpdateDTO domainsDto)
    {
        var response = await serviceManager
            .Administration.UpdateDomain(domainsDto);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpDelete("DeleteDomain/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteDomain(int id)
    {
        var response = await serviceManager
            .Administration.DeleteDomain(id);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpGet("GetTracks/{domainId:int}")]
    public async Task<ActionResult<APIResponse>> GetTracks(int domainId)
    {
        var response = await serviceManager
            .Administration.GetTracks(domainId);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpGet("GetTrack/{id:int}")]
    public async Task<ActionResult<APIResponse>> GetTrack(int id)
    {
        var response = await serviceManager
            .Administration.GetFullTrack(id);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpPost("AddTrack")]
    public async Task<ActionResult<APIResponse>> AddTrack([FromBody] TrackCreateFullDTO trackCreateDto)
    {
        var response = await serviceManager
            .Administration.AddTrackFull(trackCreateDto);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpPut("UpdateTrack")]
    public async Task<ActionResult<APIResponse>> UpdateTrack([FromBody] TrackUpdateFullDTO trackUpdateDto)
    {
        var response = await serviceManager
            .Administration.UpdateFullTrack(trackUpdateDto);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpDelete("DeleteTrack/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteTrack(int id)
    {
        var response = await serviceManager
            .Administration.DeleteTrack(id);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpDelete("DeleteSection/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteSection(int id)
    {
        var response = await serviceManager
            .Administration.DeleteSection(id);
        return response;
    }

    [Authorize(Roles = $"{StaticData.AdminRoleName},{StaticData.SuperAdminRoleName}")]
    [HttpDelete("DeleteChoice/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteChoice(int id)
    {
        var response = await serviceManager
            .Administration.DeleteChoice(id);
        return response;
    }

    [Authorize(Roles = StaticData.SuperAdminRoleName)]
    [HttpGet("GetAdmins")]
    public async Task<ActionResult<APIResponse>> GetAdmins(string? role)
    {
        var response = await serviceManager
            .Administration.GetAllAdminsAsync(role!);
        return response;
    }

    [Authorize(Roles = StaticData.SuperAdminRoleName)]
    [HttpPost("AddAdmin")]
    public async Task<ActionResult<APIResponse>> AddAdmin([FromBody] AdminCreateDTO adminCreateDto)
    {
        var response = await serviceManager
            .Administration.AddAdminAsync(adminCreateDto);
        return response;
    }

    [Authorize(Roles = StaticData.SuperAdminRoleName)]
    [HttpPut("UpdateAdmin")]
    public async Task<ActionResult<APIResponse>> UpdateAdmin([FromBody] AdminUpdateDTO adminUpdateDto)
    {
        var response = await serviceManager
            .Administration.UpdateAdminAsync(adminUpdateDto);
        return response;
    }

    [Authorize(Roles = StaticData.SuperAdminRoleName)]
    [HttpDelete("DeleteAdmin/{id:guid}")]
    public async Task<ActionResult<APIResponse>> DeleteAdmin(string id)
    {
        var response = await serviceManager
            .Administration.DeleteAdmin(id);
        return response;
    }
}
