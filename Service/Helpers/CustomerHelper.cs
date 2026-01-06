using System.Text;
using Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Service.Helpers;

public class CustomerHelper(
    ILogger<CustomerHelper> logger
    , HttpClient httpClient
    , IConfiguration configuration)
{
    private readonly string _apiKey = configuration["Gemini:ApiKey"]
                                      ?? throw new InternalServerErrorException("Gemini API key is not configured");

    private readonly string _geminiApiUrl = configuration["Gemini:ApiUrl"]
                                            ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    public async Task<string> SendRequestToGemini(string prompt)
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

        logger.LogInformation($"Sending request to Gemini API");
        var response = await httpClient.PostAsync(requestUrl, requestContent);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError($"Gemini API error: {response.StatusCode}, {errorContent}");
            throw new Exception($"Gemini API returned status code {response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync();
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
            logger.LogError(ex, "Error extracting text from Gemini response");
            throw new Exception("Failed to parse Gemini AI response", ex);
        }
    }
}