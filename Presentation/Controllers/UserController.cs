using System.Net;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared;
using Shared.DTOs.CustomerDTOs;

namespace Presintation.Controllers;
public class UserController(
    ILogger<UserController> logger,
    IServiceManager serviceManager)
    : ApiController
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
            var response = await serviceManager.User.ProcessUserQueryAsync(request.Query);

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

    [HttpGet("GetCustomerProfile/{customerId:guid}")]
    public async Task<ActionResult<APIResponse>> GetCustomerProfile(string customerId)
    {
        try
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return BadRequest(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "CustomerId cannot be Null or Empty" }
                });
            }

            var customer = await serviceManager.Customer
                .GetALlCustomerDetailsAsync(customerId);
            
            if (customer == null)
            {
                return NotFound(new APIResponse()
                {
                    IsSuccess = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessages = new List<string>() { "CustomerId cannot be Null or Empty" }
                });
            }
            return Ok(new APIResponse<CustomerDTO>()
            {
                StatusCode = HttpStatusCode.OK,
                Data = customer,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new APIResponse()
            {
                IsSuccess = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorMessages = new List<string>() { "An error occurred while processing your query."}
            });
        }
    }
}