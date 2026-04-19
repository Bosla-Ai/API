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

    public MistralOptions Mistral { get; set; } = new();

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
    public string Model { get; set; } = "gemini-3-flash-preview";
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
    public string Model { get; set; } = "qwen-3-235b-a22b-instruct-2507";
    public string ChatModel { get; set; } = "qwen-3-235b-a22b-instruct-2507";
    public string ReasoningModel { get; set; } = "qwen-3-235b-a22b-instruct-2507";
    public int MinimalInputWordThreshold { get; set; } = 3;
    public int SummarizationThreshold { get; set; } = 10;
    public int ContextMaxMessageLength { get; set; } = 8000;
    public int ContextCompactionCharThreshold { get; set; } = 30000;

    // Smart Discovery Funnel settings
    public int NewSessionMessageThreshold { get; set; } = 3;
    public bool EnableModeClassification { get; set; } = true;
    public bool EnableBackgroundProfileExtraction { get; set; } = true;
}

public class GroqOptions
{
    public List<string> ApiKeys { get; set; } = [];
    public string ApiUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    public string Model { get; set; } = "openai/gpt-oss-120b";
}

public class MistralOptions
{
    public List<string> ApiKeys { get; set; } = [];
    public string ApiUrl { get; set; } = "https://api.mistral.ai/v1/chat/completions";
    public string Model { get; set; } = "mistral-small-2506";
}

public class PromptOptions
{
    public string IntentDetectionSystemPrompt { get; set; } = string.Empty;
    public string IntentDetectionUserPromptTemplate { get; set; } = string.Empty;
    public string ChatSystemPrompt { get; set; } = string.Empty;
    public string ChatSystemPromptDeep { get; set; } = string.Empty;
    public string ChatUserPromptTemplate { get; set; } = string.Empty;
    public string SummarizationPromptTemplate { get; set; } = string.Empty;
    public string LanguageRules { get; set; } = string.Empty;

    // Smart Discovery Funnel prompts
    public string ModeClassificationPrompt { get; set; } = string.Empty;
    public string SimplifiedIntentPrompt { get; set; } = string.Empty;
    public string ProfileExtractionPrompt { get; set; } = string.Empty;
}
