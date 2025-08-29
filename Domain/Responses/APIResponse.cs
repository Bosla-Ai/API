using System.Net;

namespace Domain.Responses;

public class APIResponse
{
    public bool IsSuccess { get; set; }
    public HttpStatusCode StatusCode { get; set; }
    public object Data { get; set; }
    public List<string> ErrorMessages { get; set; }
    
    public APIResponse(){}

    public APIResponse(HttpStatusCode statusCode, object data = null, List<string> errors = null)
    {
        StatusCode = statusCode;
        Data = data;
        ErrorMessages = errors ?? new List<string>();
        IsSuccess = (int)statusCode >= 200 && (int)statusCode < 300;
    }
}

public sealed class APIResponse<T> : APIResponse
{
    public new T Data { get; set; }
    
    public APIResponse() : base() {}

    public APIResponse(HttpStatusCode statusCode, T data = default, List<string> errors = null)
        : base(statusCode, data, errors)
    {
        Data = data;
    }
}