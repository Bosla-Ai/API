using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class AiOptions
{
    public const string SectionName = "AI";

    [Required]
    public GeminiOptions Gemini { get; set; } = new();

    [Required]
    public LlmOptions Llm { get; set; } = new();

    [Required]
    public PromptOptions Prompts { get; set; } = new();

    public PipelineApiOptions PipelineApi { get; set; } = new();
}

public class PipelineApiOptions
{
    public string BaseUrl { get; set; } = "http://localhost:7860/generate-roadmap";


    public string VideoSearchUrl
    {
        get
        {
            var uri = new Uri(BaseUrl);
            return $"{uri.Scheme}://{uri.Authority}/search-embeddable-video";
        }
    }
}

public class GeminiOptions
{
    public List<string> ApiKeys { get; set; } = [];
    public string Model { get; set; } = "gemini-3.1-flash-preview";
    public string ApiUrl { get; set; } = string.Empty;
    public bool IncludeThoughts { get; set; } = true;

    // Per-user daily request limit (easy to change via .env or appsettings)
    public int MaxRequestsPerUserPerDay { get; set; } = 5;
}

public class LlmOptions
{
    public string Provider { get; set; } = "openrouter";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string Model { get; set; } = "qwen/qwen3-coder:free";
    public bool IncludeReasoning { get; set; } = false;

    // Task-based model routing
    public string IntentModel { get; set; } = "qwen/qwen3-coder:free";
    public string ChatModel { get; set; } = "qwen/qwen3-next-80b-a3b-instruct:free";
    public string ReasoningModel { get; set; } = "deepseek/deepseek-r1-0528:free";
}

public class PromptOptions
{
    public string IntentDetectionSystemPrompt { get; set; } = string.Empty;
    public string IntentDetectionUserPromptTemplate { get; set; } = string.Empty;
    public string ChatSystemPrompt { get; set; } = string.Empty;
    public string ChatUserPromptTemplate { get; set; } = string.Empty;
    public string SummarizationPromptTemplate { get; set; } = string.Empty;
}
