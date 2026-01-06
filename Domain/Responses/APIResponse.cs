using System.Net;
using System.Text.Json.Serialization;

namespace Domain.Responses;

public class APIResponse
{
    public HttpStatusCode StatusCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsSuccess => ((int)StatusCode >= 200 && (int)StatusCode < 300);

    public List<string> ErrorMessages { get; set; } = new();
}

public sealed class APIResponse<T> : APIResponse
{
    public T Data { get; set; } = default!;

    public APIResponse() { }

    public APIResponse(HttpStatusCode statusCode, T data = default, List<string>? errors = null)
    {
        StatusCode = statusCode;
        Data = data!;
        ErrorMessages = errors ?? new List<string>();
    }
}