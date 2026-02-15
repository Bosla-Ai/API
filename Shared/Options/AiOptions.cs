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
}

public class GeminiOptions
{
    public List<string> ApiKeys { get; set; } = [];
    public string Model { get; set; } = "gemini-3-flash-preview";
    public string ApiUrl { get; set; } = string.Empty;
    public bool IncludeThoughts { get; set; } = true;
}

public class LlmOptions
{
    public string Provider { get; set; } = "openrouter";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string Model { get; set; } = "qwen/qwen-2.5-coder-32b-instruct";
    public bool IncludeReasoning { get; set; } = false;
}

public class PromptOptions
{
    public string IntentDetectionSystemPrompt { get; set; } = string.Empty;
    public string IntentDetectionUserPromptTemplate { get; set; } = string.Empty;
    public string ChatSystemPrompt { get; set; } = string.Empty;
    public string ChatUserPromptTemplate { get; set; } = string.Empty;
    public string SummarizationPromptTemplate { get; set; } = string.Empty;
}
