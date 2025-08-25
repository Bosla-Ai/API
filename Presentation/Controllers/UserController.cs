using System.Net;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<APIResponse>> AskAI([FromBody] AiQueryRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() {"Query cannot be empty"}
                });
            }

            _logger.LogInformation($"Received AI query request");

            // Process the query through the UserService
            var response = await _userService.ProcessUserQueryAsync(request.Query);

            if (!response.Success)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() {response.ErrorMessage!}
                });
            }

            return Ok(new APIResponse<string>()
            {
                StatusCode = HttpStatusCode.OK,
                Data = response.Response!,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI query");
            return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { "An error occurred while processing your query."}
            });
        }
    }
}