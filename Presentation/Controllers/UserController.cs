using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared;

namespace Presintation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;
    private readonly IUserService _userService;

    public UserController(
        ILogger<UserController> logger,
        IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }

    /// Send a text query to the AI and get a response
    [HttpPost("ask-ai")]
    public async Task<IActionResult> AskAI([FromBody] AiQueryRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("Query cannot be empty");
            }

            _logger.LogInformation($"Received AI query request");

            // Process the query through the UserService
            var response = await _userService.ProcessUserQueryAsync(request.Query);

            if (!response.Success)
            {
                return BadRequest(response.ErrorMessage);
            }

            return Ok(new
            {
                response = response.Response,
                success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI query");
            return StatusCode(500, "An error occurred while processing your query.");
        }
    }
}