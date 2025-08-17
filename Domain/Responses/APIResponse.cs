using System.Net;

namespace Domain.Responses;

public class APIResponse
{
    public bool IsSuccess { get; set; } = true;
    public HttpStatusCode StatusCode { get; set; }
    public object Data { get; set; }
    public List<string> ErrorMessages { get; set; } = new List<string>(); 
}
public sealed class APIResponse<T> : APIResponse
{
    public new T Data { get; set; }
}