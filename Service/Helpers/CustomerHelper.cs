using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Domain.Exceptions;
using Google.GenAI;
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
    private readonly string _geminiModel;
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

        _geminiModel = configuration["Gemini:Model"] ?? "gemini-3-flash-preview";

        _llmProvider = configuration["LLM:Provider"]?.ToLower() ?? "openrouter";
        _llmApiKey = configuration["LLM:ApiKey"] ?? "";
        _llmApiUrl = configuration["LLM:ApiUrl"] ?? "https://openrouter.ai/api/v1/chat/completions";
        _llmModel = configuration["LLM:Model"] ?? "qwen/qwen3-coder:free";

        if (!_geminiApiKeys.Any() && string.IsNullOrWhiteSpace(_llmApiKey))
        {
            _logger.LogWarning("No AI providers configured (Gemini or LLM). Service may fail.");
        }
    }

    public async Task<(string Response, string ModelName)> SendRequestToGemini(string prompt)
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

    private async Task<(string Response, string ModelName)> ExecuteGeminiRequestWithRotation(string prompt)
    {
        for (int i = 0; i < _geminiApiKeys.Count; i++)
        {
            var keyIndex = (_currentGeminiKeyIndex + i) % _geminiApiKeys.Count;
            var currentKey = _geminiApiKeys[keyIndex];

            _logger.LogInformation("Sending request to Gemini API (Key {KeyIndex}/{TotalKeys})", keyIndex + 1, _geminiApiKeys.Count);

            try
            {
                var client = new Client(apiKey: currentKey);
                var response = await client.Models.GenerateContentAsync(
                    model: _geminiModel,
                    contents: prompt
                );

                _currentGeminiKeyIndex = keyIndex;
                var text = response.Candidates?[0].Content?.Parts?[0].Text ?? "";
                return (text, _geminiModel);
            }
            catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("quota"))
            {
                _logger.LogWarning("Gemini API Key {KeyIndex} Rate Limited (429). Switching to next key...", keyIndex + 1);
                continue;
            }
        }

        throw new InternalServerErrorException("All Gemini API keys exhausted (Rate Limited)");
    }

    private async Task<(string Response, string ModelName)> ExecuteOpenRouterRequest(string prompt)
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

        var text = ExtractTextFromOpenAIResponse(responseContent);
        return (text, _llmModel);
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

    public async IAsyncEnumerable<string> SendStreamRequestToGemini(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        bool useOpenRouter = false;

        if (_geminiApiKeys.Any())
        {
            // Try Gemini first using Channel pattern
            var channel = Channel.CreateUnbounded<string>();
            Exception? geminiException = null;
            bool geminiCompleted = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteGeminiStreamRequestWithRotation(prompt, cancellationToken))
                    {
                        await channel.Writer.WriteAsync(chunk, cancellationToken);
                    }
                    geminiCompleted = true;
                }
                catch (Exception ex)
                {
                    geminiException = ex;
                    _logger.LogWarning("Gemini streaming failed: {Error}. Will fallback to OpenRouter...", ex.Message);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return chunk;
            }

            await producerTask;

            if (geminiCompleted) yield break;

            useOpenRouter = true;
            if (geminiException != null && string.IsNullOrWhiteSpace(_llmApiKey))
            {
                throw geminiException;
            }
        }
        else
        {
            useOpenRouter = true;
        }

        // Fallback to OpenRouter
        if (useOpenRouter && !string.IsNullOrWhiteSpace(_llmApiKey))
        {
            await foreach (var chunk in ExecuteOpenRouterStreamRequest(prompt, cancellationToken))
            {
                yield return chunk;
            }
        }
        else if (useOpenRouter)
        {
            throw new InternalServerErrorException("No AI providers configured for streaming.");
        }
    }

    private async IAsyncEnumerable<string> ExecuteGeminiStreamRequestWithRotation(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Use a Channel to decouple the producer from consumer for real-time streaming
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;
        var lineBuffer = new StringBuilder();

        var producerTask = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < _geminiApiKeys.Count; i++)
                {
                    var keyIndex = (_currentGeminiKeyIndex + i) % _geminiApiKeys.Count;
                    var currentKey = _geminiApiKeys[keyIndex];

                    _logger.LogInformation("Sending STREAM request to Gemini (Key {KeyIndex}/{TotalKeys})", keyIndex + 1, _geminiApiKeys.Count);

                    try
                    {
                        var client = new Client(apiKey: currentKey);

                        // Send Model Name Protocol
                        await channel.Writer.WriteAsync($"__MODEL__:{_geminiModel}\n", cancellationToken);

                        await foreach (var chunk in client.Models.GenerateContentStreamAsync(
                            model: _geminiModel,
                            contents: prompt).WithCancellation(cancellationToken))
                        {
                            var text = chunk.Candidates?[0].Content?.Parts?[0].Text;
                            if (string.IsNullOrEmpty(text)) continue;

                            // Process characters to handle Status Protocol (>>>)
                            foreach (var c in text)
                            {
                                if (c == '\n')
                                {
                                    var line = lineBuffer.ToString();
                                    lineBuffer.Clear();

                                    if (line.TrimStart().StartsWith(">>>"))
                                    {
                                        var cleanLine = line.TrimStart().Substring(3).Trim();
                                        await channel.Writer.WriteAsync($"__STATUS__:{cleanLine}", cancellationToken);
                                    }
                                    else
                                    {
                                        await channel.Writer.WriteAsync(line + "\n", cancellationToken);
                                    }
                                }
                                else
                                {
                                    lineBuffer.Append(c);
                                }
                            }
                        }

                        // Flush remaining buffer
                        if (lineBuffer.Length > 0)
                        {
                            var line = lineBuffer.ToString();
                            if (line.TrimStart().StartsWith(">>>"))
                            {
                                await channel.Writer.WriteAsync($"__STATUS__:{line.TrimStart().Substring(3).Trim()}", cancellationToken);
                            }
                            else
                            {
                                await channel.Writer.WriteAsync(line, cancellationToken);
                            }
                        }

                        _currentGeminiKeyIndex = keyIndex;
                        return; // Success
                    }
                    catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("quota"))
                    {
                        _logger.LogWarning("Gemini Stream Key {KeyIndex} Rate Limited. Switching...", keyIndex + 1);
                        lineBuffer.Clear();
                        continue;
                    }
                }

                streamException = new InternalServerErrorException("All Gemini keys failed or were rate-limited.");
            }
            catch (Exception ex)
            {
                streamException = ex;
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Consumer: yield from channel in real-time
        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }

        await producerTask;

        if (streamException != null)
        {
            throw streamException;
        }
    }

    private async IAsyncEnumerable<string> ExecuteOpenRouterStreamRequest(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;

        var producerTask = Task.Run(async () =>
        {
            var lineBuffer = new StringBuilder();

            try
            {
                var requestBody = new
                {
                    model = _llmModel,
                    messages = new[] { new { role = "user", content = prompt } },
                    stream = true
                };

                var requestJson = JsonConvert.SerializeObject(requestBody);
                var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, _llmApiUrl);
                request.Content = requestContent;
                request.Headers.Add("Authorization", $"Bearer {_llmApiKey}");
                request.Headers.Add("HTTP-Referer", "https://bosla.me");
                request.Headers.Add("X-Title", "Bosla AI");

                _logger.LogInformation("Sending STREAM request to {Provider} (Fallback) with model {Model}", _llmProvider, _llmModel);

                // Send Model Name Protocol
                await channel.Writer.WriteAsync($"__MODEL__:{_llmModel}\n", cancellationToken);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new InternalServerErrorException($"OpenRouter Stream Error {response.StatusCode}: {errorContent}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith(":")) continue;

                    if (!line.StartsWith("data: ")) continue;

                    var jsonData = line.Substring(6);
                    if (jsonData == "[DONE]") break;

                    try
                    {
                        var chunk = JsonConvert.DeserializeObject<dynamic>(jsonData);

                        if (chunk?.error != null)
                        {
                            string errorMessage = (string)(chunk.error.message?.ToString() ?? "Unknown error");
                            _logger.LogWarning("OpenRouter mid-stream error: {Error}", errorMessage);
                            throw new InternalServerErrorException($"OpenRouter stream error: {errorMessage}");
                        }

                        var content = chunk?.choices?[0]?.delta?.content?.ToString();

                        if (string.IsNullOrEmpty(content)) continue;

                        // Process characters to handle Status Protocol (>>>)
                        foreach (var c in content)
                        {
                            if (c == '\n')
                            {
                                var bufferLine = lineBuffer.ToString();
                                lineBuffer.Clear();

                                if (bufferLine.TrimStart().StartsWith(">>>"))
                                {
                                    var cleanLine = bufferLine.TrimStart().Substring(3).Trim();
                                    await channel.Writer.WriteAsync($"__STATUS__:{cleanLine}", cancellationToken);
                                }
                                else
                                {
                                    await channel.Writer.WriteAsync(bufferLine + "\n", cancellationToken);
                                }
                            }
                            else
                            {
                                lineBuffer.Append(c);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed JSON chunks
                    }
                }

                // Flush remaining buffer
                if (lineBuffer.Length > 0)
                {
                    var bufferLine = lineBuffer.ToString();
                    if (bufferLine.TrimStart().StartsWith(">>>"))
                    {
                        await channel.Writer.WriteAsync($"__STATUS__:{bufferLine.TrimStart().Substring(3).Trim()}", cancellationToken);
                    }
                    else
                    {
                        await channel.Writer.WriteAsync(bufferLine, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                streamException = ex;
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Consumer: yield from channel in real-time
        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }

        await producerTask;

        if (streamException != null)
        {
            throw streamException;
        }
    }

    public async Task<string> SummarizeConversationAsync(string conversationHistory)
    {
        var prompt = $@"Summarize the following conversation into a concise summary that captures the key points, user preferences, and important context. Keep it under 200 words.

Conversation:
{conversationHistory}

Provide a clear, factual summary:";

        var result = await SendRequestToGemini(prompt);
        return result.Response;
    }
}