using System.Net;
using Domain.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Filters;

public class ApiResponseFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context
        , ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult)
        {
            var statusCode = (HttpStatusCode)(objectResult.StatusCode ?? StatusCodes.Status200OK);

            var wrapped = new APIResponse<object>(
                statusCode,
                objectResult.Value,
                null
            );

            context.Result = new ObjectResult(wrapped)
            {
                StatusCode = (int)statusCode
            };
        }
        await next();
    }
}