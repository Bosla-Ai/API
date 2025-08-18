using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Service.Abstraction;
using Shared;

namespace Service.Implementations;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;
    private readonly string? _apiKey;
    private readonly string _geminiApiUrl;

    public UserService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["Gemini:ApiKey"];
        _geminiApiUrl = configuration["Gemini:ApiUrl"] ??
                        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new ArgumentNullException(nameof(_apiKey), "Gemini API key is not configured");
        }
    }

    public async Task<AiQueryResponse> ProcessUserQueryAsync(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new AiQueryResponse
                {
                    Success = false,
                    ErrorMessage = "Query cannot be empty"
                };
            }

            // Format the prompt for the AI
            string aiPrompt = $"User query: {query}\n\nPlease provide a helpful response.";

            // Send the query to Gemini API
            var geminiResponse = await SendRequestToGemini(aiPrompt);

            // Extract the text from the response
            string responseText = ExtractTextFromResponse(geminiResponse);

            return new AiQueryResponse
            {
                Response = responseText,
                Success = true,
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user query with AI");
            return new AiQueryResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while processing your query. Please try again later."
            };
        }
    }

    private async Task<string> SendRequestToGemini(string prompt)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            }
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Add API key as a query parameter
        string requestUrl = $"{_geminiApiUrl}?key={_apiKey}";

        _logger.LogInformation($"Sending request to Gemini API");
        var response = await _httpClient.PostAsync(requestUrl, requestContent);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Gemini API error: {response.StatusCode}, {errorContent}");
            throw new Exception($"Gemini API returned status code {response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private string ExtractTextFromResponse(string geminiResponse)
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
}
