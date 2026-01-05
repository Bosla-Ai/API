using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs.RoadmapDTOs;

namespace Presintation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RoadmapGeneratorController(
    IServiceManager serviceManager
    , IConfiguration configuration) : ApiController(configuration)
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] RoadmapRequestDTO request)
    {
        if (request.Tags == null || request.Tags.Length == 0)
            return BadRequest("At least one tag is required.");

        var result = await serviceManager.Roadmap.GenerateRoadmapAsync(
            request.Tags,
            request.Level,
            request.Language,
            request.PreferPaid
        );

        return Ok(result);
    }

    [HttpPost("save")]
    [Authorize]
    public async Task<IActionResult> SaveRoadmap([FromBody] RoadmapDTO request)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found in token.");

        var success = await serviceManager.Roadmap.SaveRoadmapAsync(userId, request);

        return Ok(success);
    }
}