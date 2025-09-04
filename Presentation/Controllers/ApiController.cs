using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Presintation.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class ApiController : ControllerBase
{
}