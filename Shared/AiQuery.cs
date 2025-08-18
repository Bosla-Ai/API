namespace Shared;

public class AiQueryRequest
{
    /// <summary>
    /// The user's text query to be processed by the AI
    /// </summary>
    public string? Query { get; set; }
}

public class AiQueryResponse
{
    /// The AI-generated response text
    public string? Response { get; set; }

    /// Indicates if the query was successfully processed
    public bool Success { get; set; }

    /// Error message if the query processing failed
    public string? ErrorMessage { get; set; }
}