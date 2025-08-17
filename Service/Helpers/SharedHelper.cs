using System.Net;
using Domain.Responses;

namespace Service.Helpers;

public static class SharedHelper
{
    public static void ResetResponse(this APIResponse response)
    {
        response.IsSuccess = true;
        response.StatusCode = HttpStatusCode.OK;
        response.Data = null;
        response.ErrorMessages = new List<string>();
    }
}