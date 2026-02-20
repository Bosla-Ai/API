using System.Security.Claims;
using System.Text.RegularExpressions;
using Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Abstraction;
using Shared.DTOs.RoadmapDTOs;

namespace Presentation.Controllers;

using Microsoft.Extensions.Options;
using Shared.Options;

[ApiController]
[Authorize]
public class RoadmapGeneratorController(
    IServiceManager serviceManager
    , IOptions<CookieSettingsOptions> cookieOptions) : ApiController(cookieOptions)
{
    // Regex to allow only alphanumeric, spaces, hyphens, and common characters
    private static readonly Regex SafeTagPattern = new(@"^[\w\s\-\.\#\+]+$", RegexOptions.Compiled);

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] RoadmapRequestDTO request)
    {
        if (request.Tags == null || request.Tags.Length == 0)
            return BadRequest("At least one tag is required.");

        // Validate and sanitize each tag
        var sanitizedTags = request.Tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Where(t => t.Length <= 50 && SafeTagPattern.IsMatch(t))
            .Take(10) // Extra safety: limit to 10 tags
            .ToArray();

        if (sanitizedTags.Length == 0)
            return BadRequest("No valid tags provided. Tags must be alphanumeric and up to 50 characters.");

        var result = await serviceManager.Roadmap.GenerateRoadmapAsync(
            sanitizedTags,
            request.Language,
            request.PreferPaid
        );

        return Ok(result);
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveRoadmap([FromBody] RoadmapDTO request)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found in token.");

        var success = await serviceManager.Roadmap.SaveRoadmapAsync(userId, request);

        return Ok(success);
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetAllRoadmaps()
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found in token.");

        var result = await serviceManager.Roadmap.GetAllUserRoadmapsAsync(userId);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRoadmapDetails(int id)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found in token.");

        var result = await serviceManager.Roadmap.GetRoadmapDetailsAsync(id, userId);
        return Ok(result);
    }

    [HttpDelete("delete/{id:int}")]
    public async Task<ActionResult<APIResponse>> DeleteRoadmap(int id)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        var response = await serviceManager.Roadmap.DeleteRoadmapAsync(id, userId);

        return Ok(response);
    }
}
