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
    private readonly List<string> _apiKeys;
    private readonly string _geminiApiUrl;

    private static volatile int _currentKeyIndex = 0;

    public CustomerHelper(ILogger<CustomerHelper> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;

        var keysString = configuration["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(keysString))
            throw new InternalServerErrorException("Gemini API key is not configured");

        _apiKeys = keysString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        if (!_apiKeys.Any())
            throw new InternalServerErrorException("No valid Gemini API keys found");

        _geminiApiUrl = configuration["Gemini:ApiUrl"]
                        ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";
    }

    public async Task<string> SendRequestToGemini(string prompt)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        Exception? lastException = null;

        // Try usage of keys in a loop
        for (int i = 0; i < _apiKeys.Count; i++)
        {
            // Simple round-robin for the current attempt sequence
            var keyIndex = (_currentKeyIndex + i) % _apiKeys.Count;
            var currentKey = _apiKeys[keyIndex];

            string requestUrl = $"{_geminiApiUrl}?key={currentKey}";

            _logger.LogInformation($"Sending request to Gemini API (Key {keyIndex + 1}/{_apiKeys.Count})");

            var response = await _httpClient.PostAsync(requestUrl, requestContent);

            if (response.IsSuccessStatusCode)
            {
                // If successful, update the main index preference to this working key
                _currentKeyIndex = keyIndex;
                return await response.Content.ReadAsStringAsync();
            }

            var errorContent = await response.Content.ReadAsStringAsync();

            // Only retry on 429 (Too Many Requests)
            if ((int)response.StatusCode == 429)
            {
                _logger.LogWarning($"Gemini API Key {keyIndex + 1} Rate Limited (429). Switching to next key...");
                lastException = new Exception($"Gemini API 429: {errorContent}");
                continue; // Try next key
            }

            // For other errors, fail immediately
            _logger.LogError($"Gemini API error: {response.StatusCode} ({(int)response.StatusCode}), {errorContent}");
            throw new Exception($"Gemini API returned status code {response.StatusCode}");
        }

        throw new Exception("All Gemini API keys exhausted (Rate Limited)", lastException);
    }

    public string ExtractTextFromResponse(string geminiResponse)
    {
        try
        {
            // Parse the Gemini response to extract the generated text
            var responseObject = JsonConvert.DeserializeObject<dynamic>(geminiResponse);
            var text = responseObject!.candidates[0].content.parts[0].text.ToString();

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from Gemini response");
            throw new Exception("Failed to parse Gemini AI response", ex);
        }
    }

    public async Task<string> SummarizeConversationAsync(string conversationHistory)
    {
        var prompt = $@"Summarize the following conversation into a concise summary that captures the key points, user preferences, and important context. Keep it under 200 words.

Conversation:
{conversationHistory}

Provide a clear, factual summary:";

        var response = await SendRequestToGemini(prompt);
        return ExtractTextFromResponse(response);
    }
}