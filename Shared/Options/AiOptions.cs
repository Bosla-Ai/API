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
    public GroqOptions Groq { get; set; } = new();

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
    public string Model { get; set; } = "gemini-3.1-flash-lite-preview";
    public List<string> FallbackModels { get; set; } = [];
    public string ApiUrl { get; set; } = string.Empty;
    public bool IncludeThoughts { get; set; } = true;
    public int MaxRequestsPerUserPerDay { get; set; } = 5;
}

public class LlmOptions
{
    public string Provider { get; set; } = "cerebras";
    public List<string> ApiKeys { get; set; } = [];
    public string ApiUrl { get; set; } = "https://api.cerebras.ai/v1/chat/completions";
    public string Model { get; set; } = "gpt-oss-120b";
    public string ChatModel { get; set; } = "gpt-oss-120b";
    public string ReasoningModel { get; set; } = "gpt-oss-120b";
}

public class GroqOptions
{
    public List<string> ApiKeys { get; set; } = [];
    public string ApiUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    public string Model { get; set; } = "openai/gpt-oss-120b";
}

public class PromptOptions
{
    public string IntentDetectionSystemPrompt { get; set; } = string.Empty;
    public string IntentDetectionUserPromptTemplate { get; set; } = string.Empty;
    public string ChatSystemPrompt { get; set; } = string.Empty;
    public string ChatUserPromptTemplate { get; set; } = string.Empty;
    public string SummarizationPromptTemplate { get; set; } = string.Empty;
}
