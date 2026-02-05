namespace Shared;

public class AiQueryRequest
{
    public string? Query { get; set; }

    public string? SessionId { get; set; }
}

public class AiQueryResponse
{
    public string? Response { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }
}