using System.Text;
using Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Service.Helpers;

public class CustomerHelper
{
    private readonly ILogger<CustomerHelper> _logger;
    private readonly HttpClient _httpClient;

    // Gemini 
    private readonly List<string> _geminiApiKeys;
    private readonly string _geminiApiUrl;
    private static volatile int _currentGeminiKeyIndex = 0;

    // OpenRouter
    private readonly string _llmApiKey;
    private readonly string _llmApiUrl;
    private readonly string _llmModel;
    private readonly string _llmProvider;

    public CustomerHelper(ILogger<CustomerHelper> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;

        var geminiKeysString = configuration["Gemini:ApiKey"];
        _geminiApiKeys = !string.IsNullOrEmpty(geminiKeysString)
            ? geminiKeysString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : new List<string>();

        _geminiApiUrl = configuration["Gemini:ApiUrl"]
                        ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent";

        _llmProvider = configuration["LLM:Provider"]?.ToLower() ?? "openrouter";
        _llmApiKey = configuration["LLM:ApiKey"] ?? "";
        _llmApiUrl = configuration["LLM:ApiUrl"] ?? "https://openrouter.ai/api/v1/chat/completions";
        _llmModel = configuration["LLM:Model"] ?? "qwen/qwen3-coder:free";

        if (!_geminiApiKeys.Any() && string.IsNullOrWhiteSpace(_llmApiKey))
        {
            _logger.LogWarning("No AI providers configured (Gemini or LLM). Service may fail.");
        }
    }

    public async Task<string> SendRequestToGemini(string prompt)
    {
        if (_geminiApiKeys.Any())
        {
            try
            {
                return await ExecuteGeminiRequestWithRotation(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini Primary Provider Failed. Attempting Fallback to {Provider}...", _llmProvider);
            }
        }
        else
        {
            _logger.LogWarning("Gemini keys not configured. Skipping to Fallback provider.");
        }

        if (!string.IsNullOrWhiteSpace(_llmApiKey))
        {
            try
            {
                return await ExecuteOpenRouterRequest(prompt);
            }
            catch (Exception ex)
            {
                throw new InternalServerErrorException($"Fallback Provider ({_llmProvider}) Failed: {ex.Message}");
            }
        }

        throw new InternalServerErrorException("All AI providers failed or are not configured.");
    }

    private async Task<string> ExecuteGeminiRequestWithRotation(string prompt)
    {
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        Exception? lastException = null;

        for (int i = 0; i < _geminiApiKeys.Count; i++)
        {
            var keyIndex = (_currentGeminiKeyIndex + i) % _geminiApiKeys.Count;
            var currentKey = _geminiApiKeys[keyIndex];
            string requestUrl = $"{_geminiApiUrl}?key={currentKey}";

            _logger.LogInformation($"Sending request to Gemini API (Key {keyIndex + 1}/{_geminiApiKeys.Count})");

            var response = await _httpClient.PostAsync(requestUrl, requestContent);

            if (response.IsSuccessStatusCode)
            {
                _currentGeminiKeyIndex = keyIndex; // Update preference to working key
                var json = await response.Content.ReadAsStringAsync();
                return ExtractTextFromGeminiResponse(json);
            }

            var errorContent = await response.Content.ReadAsStringAsync();

            if ((int)response.StatusCode == 429)
            {
                _logger.LogWarning($"Gemini API Key {keyIndex + 1} Rate Limited (429). Switching to next key...");
                lastException = new InternalServerErrorException($"Gemini 429: {errorContent}");
                continue;
            }

            throw new InternalServerErrorException($"Gemini Status {response.StatusCode}: {errorContent}");
        }

        throw new InternalServerErrorException("All Gemini API keys exhausted (Rate Limited)");
    }

    private async Task<string> ExecuteOpenRouterRequest(string prompt)
    {
        var requestBody = new
        {
            model = _llmModel,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _llmApiUrl);
        request.Content = requestContent;
        request.Headers.Add("Authorization", $"Bearer {_llmApiKey}");
        request.Headers.Add("HTTP-Referer", "https://bosla.me");
        request.Headers.Add("X-Title", "Bosla AI");

        _logger.LogInformation("Sending request to {Provider} (Fallback) with model {Model}", _llmProvider, _llmModel);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException($"LLM API Error {response.StatusCode}: {responseContent}");
        }

        return ExtractTextFromOpenAIResponse(responseContent);
    }

    private string ExtractTextFromGeminiResponse(string json)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(json);
            return obj!.candidates[0].content.parts[0].text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini JSON");
            throw;
        }
    }

    private string ExtractTextFromOpenAIResponse(string json)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(json);
            return obj!.choices[0].message.content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenRouter/LLM JSON");
            throw;
        }
    }

    public string ExtractTextFromResponse(string responseJson)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(responseJson);
            if (obj!.candidates != null)
            {
                return obj.candidates[0].content.parts[0].text.ToString();
            }
            if (obj!.choices != null)
            {
                return obj.choices[0].message.content.ToString();
            }
        }
        catch { /* ignore and throw below */ }

        _logger.LogError("Unknown JSON format or parsing error: {Json}", responseJson);
        throw new BadRequestException("Failed to parse AI response (Unknown format)");
    }

    public async Task<string> SummarizeConversationAsync(string conversationHistory)
    {
        var prompt = $@"Summarize the following conversation into a concise summary that captures the key points, user preferences, and important context. Keep it under 200 words.

Conversation:
{conversationHistory}

Provide a clear, factual summary:";

        return await SendRequestToGemini(prompt);
    }
}