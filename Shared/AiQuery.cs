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

public record AiRequestIdResponse(string RequestId);

public class CancelChatRequest
{
    public string? PartialResponse { get; set; }
}

public class AskUserQuestion
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "checkbox"; // "checkbox" | "text"
    public string[]? Options { get; set; }
    public string? Placeholder { get; set; }
    public bool Required { get; set; } = true;
}