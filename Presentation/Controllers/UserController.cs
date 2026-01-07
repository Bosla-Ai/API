using System.Net;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared;

namespace Presentation.Controllers;

public class UserController(
    ILogger<UserController> logger,
    IServiceManager serviceManager,
    IConfiguration configuration)
    : ApiController(configuration)
{
    /// Send a text query to the AI and get a response
    [HttpPost("ask-ai")]
    public async Task<ActionResult<APIResponse<string>>> AskAI([FromBody] AiQueryRequest request)
    {
        var response = await serviceManager.Customer
            .ProcessUserQueryAsync(request.Query!);
        return Ok(response);
    }

    [HttpGet("GetCustomerProfile/{customerId:guid}")]
    public async Task<ActionResult<APIResponse>> GetCustomerProfile(string customerId)
    {
        var response = await serviceManager.Customer
            .GetCustomerProfileAsync(customerId);
        return Ok(response);
    }
}