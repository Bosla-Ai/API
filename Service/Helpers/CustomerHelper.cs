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
    private readonly string _apiKey;
    private readonly string _apiUrl;
    private readonly string _model;
    private readonly string _provider;

    public CustomerHelper(ILogger<CustomerHelper> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;

        _provider = configuration["LLM:Provider"]?.ToLower() ?? "openrouter";
        _apiKey = configuration["LLM:ApiKey"] 
            ?? throw new InternalServerErrorException("LLM:ApiKey is not configured");
        _apiUrl = configuration["LLM:ApiUrl"] 
            ?? "https://openrouter.ai/api/v1/chat/completions";
        _model = configuration["LLM:Model"] 
            ?? "qwen/qwen3-coder:free";

        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InternalServerErrorException("LLM:ApiKey cannot be empty");
    }

    public async Task<string> SendRequestToLLM(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
        request.Content = requestContent;
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "https://bosla.me");
        request.Headers.Add("X-Title", "Bosla AI");

        _logger.LogInformation("Sending request to {Provider} API with model {Model}", _provider, _model);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("LLM API error: {StatusCode}, {Content}", response.StatusCode, responseContent);
            
            if ((int)response.StatusCode == 429)
            {
                throw new Exception($"LLM API rate limited (429): {responseContent}");
            }
            
            throw new Exception($"LLM API returned status code {response.StatusCode}: {responseContent}");
        }

        return responseContent;
    }

    // Backward compatibility - rename later
    public Task<string> SendRequestToGemini(string prompt) => SendRequestToLLM(prompt);

    public string ExtractTextFromResponse(string llmResponse)
    {
        try
        {
            var responseObject = JsonConvert.DeserializeObject<dynamic>(llmResponse);
            
            // OpenRouter/OpenAI format: choices[0].message.content
            var text = responseObject!.choices[0].message.content.ToString();
            
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from LLM response: {Response}", 
                llmResponse?.Substring(0, Math.Min(500, llmResponse?.Length ?? 0)));
            throw new Exception("Failed to parse LLM response", ex);
        }
    }

    public async Task<string> SummarizeConversationAsync(string conversationHistory)
    {
        var prompt = $@"Summarize the following conversation into a concise summary that captures the key points, user preferences, and important context. Keep it under 200 words.

Conversation:
{conversationHistory}

Provide a clear, factual summary:";

        var response = await SendRequestToLLM(prompt);
        return ExtractTextFromResponse(response);
    }
}