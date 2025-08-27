using System.Net;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared;

namespace Presintation.Controllers;

public class UserController(
    ILogger<UserController> logger,
    IUserService userService)
    : ControllerBase
{
    /// Send a text query to the AI and get a response
    [HttpPost("ask-ai")]
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
                    ErrorMessages = new List<string>() { "Query cannot be empty" }
                });
            }

            logger.LogInformation($"Received AI query request");

            // Process the query through the UserService
            var response = await userService.ProcessUserQueryAsync(request.Query);

            if (!response.Success)
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { response.ErrorMessage! }
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
            logger.LogError(ex, "Error processing AI query");
            return StatusCode((int)HttpStatusCode.InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { "An error occurred while processing your query." }
            });
        }
    }
}