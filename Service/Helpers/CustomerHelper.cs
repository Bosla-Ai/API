using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Shared.Enums;
using Shared.Options;

namespace Service.Helpers;

public class CustomerHelper
{
    private readonly ILogger<CustomerHelper> _logger;
    private readonly HttpClient _httpClient;
    private readonly UserRateLimiter _rateLimiter;

    // Gemini 
    private readonly List<string> _geminiApiKeys;
    private readonly string _geminiModel;
    private readonly string _geminiApiUrl;
    private readonly bool _geminiIncludeThoughts;
    private static volatile int _currentGeminiKeyIndex = 0;

    // OpenRouter
    private readonly string _llmApiKey;
    private readonly string _llmApiUrl;
    private readonly string _llmModel;
    private readonly string _llmProvider;
    private readonly bool _llmIncludeReasoning;

    // Task-based model routing
    private readonly string _intentModel;
    private readonly string _chatModel;
    private readonly string _reasoningModel;
    private readonly List<string> _fallbackModels;

    private bool IsReasoningModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return false;
        var name = modelName.ToLowerInvariant();
        return name.Contains("thinking") ||
               name.Contains("reasoning") ||
               name.Contains("deepseek-r1") ||
               name.Contains("o1") ||
               name.Contains("o3");
    }

    public CustomerHelper(ILogger<CustomerHelper> logger, HttpClient httpClient, IOptionsMonitor<AiOptions> options, UserRateLimiter rateLimiter)
    {
        _logger = logger;
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        var aiOptions = options.CurrentValue;

        _geminiApiKeys = aiOptions.Gemini.ApiKeys;
        _geminiModel = aiOptions.Gemini.Model;
        _geminiApiUrl = aiOptions.Gemini.ApiUrl;
        _geminiIncludeThoughts = aiOptions.Gemini.IncludeThoughts;

        _llmProvider = aiOptions.Llm.Provider;
        _llmApiKey = aiOptions.Llm.ApiKey;
        _llmApiUrl = aiOptions.Llm.ApiUrl;
        _llmModel = aiOptions.Llm.Model;
        _llmIncludeReasoning = aiOptions.Llm.IncludeReasoning;

        _intentModel = aiOptions.Llm.IntentModel;
        _chatModel = aiOptions.Llm.ChatModel;
        _reasoningModel = aiOptions.Llm.ReasoningModel;
        _fallbackModels = aiOptions.Llm.FallbackModels ?? [];

        if (!_geminiApiKeys.Any() && string.IsNullOrWhiteSpace(_llmApiKey))
        {
            _logger.LogWarning("No AI providers configured (Gemini or LLM). Service may fail.");
        }
    }

    public string GetModelForTask(LLMInteractionType taskType) => taskType switch
    {
        LLMInteractionType.ChatWithAI => _chatModel,
        LLMInteractionType.ChooseTrack => _chatModel,
        LLMInteractionType.ChooseMethod => _chatModel,
        LLMInteractionType.TopicPreview => _chatModel,
        LLMInteractionType.RoadmapGeneration => _reasoningModel,
        LLMInteractionType.CVAnalysis => _reasoningModel,
        _ => _llmModel
    };

    /// <summary>
    /// Returns the number of remaining Gemini requests for the given user today.
    /// </summary>
    public int GetGeminiRemainingQuota(string userId, bool isSuperAdmin)
        => _rateLimiter.GetRemainingRequests(userId, isSuperAdmin);

    public async Task<(string Response, string ModelName, string? ThinkingContent)> SendRequestToGemini(string prompt, bool useThinking = false, string? userId = null, bool isSuperAdmin = false)
    {
        if (_geminiApiKeys.Any())
        {
            if (userId != null && !_rateLimiter.TryConsumeRequest(userId, isSuperAdmin))
            {
                _logger.LogWarning("User {UserId} exceeded daily Gemini rate limit. Routing to OpenRouter.", userId);
            }
            else
            {
                try
                {
                    return await ExecuteGeminiRequestWithRotation(prompt, useThinking);
                }
                catch (Exception ex)
                {
                    // Gemini failed — but quota was already consumed. This is acceptable because
                    // we need to know intent BEFORE calling, and the attempt still costs us API usage.
                    _logger.LogWarning(ex, "Gemini Primary Provider Failed. Attempting Fallback to {Provider}...", _llmProvider);
                }
            }
        }
        else
        {
            _logger.LogWarning("Gemini keys not configured. Skipping to Fallback provider.");
        }

        // Fallback: loop through fallback models before giving up
        return await ExecuteOpenRouterWithFallbackChain(prompt, useThinking);
    }

    public async Task<(string Response, string ModelName, string? ThinkingContent)> SendRequestByTask(
        string prompt, LLMInteractionType taskType, bool useThinking = false,
        string? userId = null, bool isSuperAdmin = false, ChatMode chatMode = ChatMode.Normal)
    {
        var targetModel = GetModelForTask(taskType);

        // Powerful mode: route chat tasks to Gemini
        if (chatMode == ChatMode.Powerful &&
            (taskType == LLMInteractionType.ChatWithAI || taskType == LLMInteractionType.ChooseTrack
             || taskType == LLMInteractionType.ChooseMethod || taskType == LLMInteractionType.TopicPreview))
        {
            return await SendRequestToGemini(prompt, useThinking, userId, isSuperAdmin);
        }

        // Reasoning tasks (CV, Roadmap) always use DeepSeek R1 via OpenRouter — even in Powerful mode,
        // because the reasoning model outperforms Gemini on long-form analytical tasks.
        if (taskType == LLMInteractionType.RoadmapGeneration || taskType == LLMInteractionType.CVAnalysis)
        {
            return await ExecuteOpenRouterWithFallbackChain(prompt, useThinking, targetModel);
        }

        // Normal mode: route everything through OpenRouter fallback chain
        return await ExecuteOpenRouterWithFallbackChain(prompt, useThinking, targetModel);
    }

    public async Task<(string Response, string ModelName, string? ThinkingContent)> SendRequestWithModel(
        string prompt, string model, bool useThinking = false, string? userId = null, bool isSuperAdmin = false)
    {
        return await ExecuteOpenRouterWithFallbackChain(prompt, useThinking, model);
    }

    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteOpenRouterWithFallbackChain(
        string prompt, bool useThinking, string? primaryModel = null)
    {
        if (string.IsNullOrWhiteSpace(_llmApiKey))
            throw new InternalServerErrorException("OpenRouter API key is not configured.");

        var modelsToTry = new List<string>();
        if (!string.IsNullOrWhiteSpace(primaryModel))
            modelsToTry.Add(primaryModel);

        foreach (var fb in _fallbackModels)
        {
            if (!modelsToTry.Contains(fb, StringComparer.OrdinalIgnoreCase))
                modelsToTry.Add(fb);
        }

        Exception? lastException = null;
        foreach (var model in modelsToTry)
        {
            try
            {
                return await ExecuteOpenRouterRequest(prompt, useThinking, model);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "OpenRouter model {Model} failed. Trying next fallback...", model);
            }
        }

        throw new InternalServerErrorException(
            $"All OpenRouter models failed. Last error: {lastException?.Message}", lastException);
    }

    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteGeminiRequestWithRotation(string prompt, bool useThinking)
    {
        for (int i = 0; i < _geminiApiKeys.Count; i++)
        {
            var keyIndex = (_currentGeminiKeyIndex + i) % _geminiApiKeys.Count;
            var currentKey = _geminiApiKeys[keyIndex];

            _logger.LogInformation("Sending request to Gemini API (Key {KeyIndex}/{TotalKeys})", keyIndex + 1, _geminiApiKeys.Count);

            try
            {
                var url = $"{_geminiApiUrl}?key={currentKey}";

                var enableThinking = (_geminiIncludeThoughts || IsReasoningModel(_geminiModel)) && useThinking;

                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = enableThinking ? new { thinking_config = new { include_thoughts = true } } : null
                };

                var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Gemini Error {response.StatusCode}: {responseString}");
                }

                _currentGeminiKeyIndex = keyIndex;

                // Parse response to find text and thoughts
                var text = ExtractTextFromResponse(responseString, out var thought);
                return (text, _geminiModel, thought);
            }
            catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("quota"))
            {
                _logger.LogWarning("Gemini API Key {KeyIndex} Rate Limited (429). Switching to next key...", keyIndex + 1);
                continue;
            }
        }

        throw new InternalServerErrorException("All Gemini API keys exhausted (Rate Limited)");
    }

    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteOpenRouterRequest(string prompt, bool useReasoning, string? modelOverride = null)
    {
        var model = modelOverride ?? _llmModel;
        var enableReasoning = (_llmIncludeReasoning || IsReasoningModel(model)) && useReasoning;

        var requestBody = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            include_reasoning = enableReasoning
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _llmApiUrl);
        request.Content = requestContent;
        request.Headers.Add("Authorization", $"Bearer {_llmApiKey}");
        request.Headers.Add("HTTP-Referer", "https://bosla.me");
        request.Headers.Add("X-Title", "Bosla AI");

        _logger.LogInformation("Sending request to {Provider} with model {Model}", _llmProvider, model);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException($"LLM API Error {response.StatusCode}: {responseContent}");
        }

        var text = ExtractTextFromOpenAIResponse(responseContent, out var reasoning);
        return (text, model, reasoning);
    }

    private string ExtractTextFromOpenAIResponse(string json, out string? reasoning)
    {
        reasoning = null;
        try
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(json);
            if (obj == null || obj.choices == null || obj.choices.Count == 0) return string.Empty;

            var message = obj.choices?[0]?.message;
            if (message == null) return string.Empty;

            // Check for reasoning content
            if (message.reasoning != null)
            {
                reasoning = message.reasoning.ToString();
                // Optionally log reasoning/thought
                if (!string.IsNullOrWhiteSpace(reasoning))
                {
                    _logger.LogInformation("OpenRouter Reasoning: {Reasoning}", reasoning);
                }
            }

            return message.content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenRouter/LLM JSON");
            throw;
        }
    }

    public string ExtractTextFromResponse(string responseJson)
    {
        return ExtractTextFromResponse(responseJson, out _);
    }

    public string ExtractTextFromResponse(string responseJson, out string? thought)
    {
        thought = null;
        try
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(responseJson);
            if (obj != null && obj.candidates != null && obj.candidates.Count > 0)
            {
                var candidate = obj.candidates[0];
                var parts = candidate?.content?.parts;
                if (parts == null) return string.Empty;

                var sb = new StringBuilder();

                foreach (var part in parts)
                {
                    // Check for thought/reasoning in parts
                    // Note: API might return "thought": true/false in metadata, or "thought" field in part
                    // Based on user "Extract the thought field for internal logic validation"
                    if (part.thought == true || part.thought != null)
                    {
                        // Some APIs return "thought": "text..."
                        thought = part.thought.ToString(); // Or part.text if thought is just a flag
                        if (part.text != null) thought = part.text.ToString(); // If thought is a flag, text contains the thought
                        _logger.LogInformation("Gemini Thought: {Thought}", thought);
                        continue; // Don't include thought in final text
                    }

                    if (part.text != null)
                    {
                        sb.Append(part.text.ToString());
                    }
                }

                return sb.ToString();
            }
            if (obj?.choices != null && obj.choices.Count > 0)
            {
                return obj.choices[0]?.message?.content?.ToString() ?? string.Empty;
            }
        }
        catch { /* ignore and throw below */ }

        _logger.LogError("Unknown JSON format or parsing error: {Json}", responseJson);
        throw new BadRequestException("Failed to parse AI response (Unknown format)");
    }

    public async IAsyncEnumerable<string> SendStreamRequestToGemini(
        string prompt,
        bool useThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? userId = null,
        bool isSuperAdmin = false)
    {
        bool useOpenRouter = false;

        // Check per-user rate limit before Gemini
        bool rateLimited = userId != null && !_rateLimiter.TryConsumeRequest(userId, isSuperAdmin);
        if (rateLimited)
        {
            _logger.LogWarning("User {UserId} exceeded daily Gemini rate limit. Routing stream to OpenRouter.", userId);
            useOpenRouter = true;
        }

        if (!useOpenRouter && _geminiApiKeys.Any())
        {
            // Try Gemini first using Channel pattern
            var channel = Channel.CreateUnbounded<string>();
            Exception? geminiException = null;
            bool geminiCompleted = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteGeminiStreamRequestWithRotation(prompt, useThinking, cancellationToken))
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
        else if (!rateLimited)
        {
            useOpenRouter = true;
        }

        // Fallback to OpenRouter with fallback chain
        if (useOpenRouter && !string.IsNullOrWhiteSpace(_llmApiKey))
        {
            yield return "__FALLBACK__:openrouter";
            await foreach (var chunk in ExecuteOpenRouterStreamWithFallbackChain(prompt, useThinking, cancellationToken))
            {
                yield return chunk;
            }
        }
        else if (useOpenRouter)
        {
            throw new InternalServerErrorException("No AI providers configured for streaming.");
        }
    }


    public async IAsyncEnumerable<string> SendStreamRequestByTask(
        string prompt,
        LLMInteractionType taskType,
        bool useThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? userId = null,
        bool isSuperAdmin = false,
        ChatMode chatMode = ChatMode.Normal)
    {
        // Powerful mode: route chat tasks to Gemini 
        if (chatMode == ChatMode.Powerful &&
            (taskType == LLMInteractionType.ChatWithAI || taskType == LLMInteractionType.ChooseTrack
             || taskType == LLMInteractionType.ChooseMethod || taskType == LLMInteractionType.TopicPreview))
        {
            await foreach (var chunk in SendStreamRequestToGemini(prompt, useThinking, cancellationToken, userId, isSuperAdmin))
                yield return chunk;
            yield break;
        }

        // All tasks route to OpenRouter with task-specific model + fallback chain
        var targetModel = GetModelForTask(taskType);
        await foreach (var chunk in ExecuteOpenRouterStreamWithFallbackChain(prompt, useThinking, cancellationToken, targetModel))
            yield return chunk;
    }

    public async IAsyncEnumerable<string> SendStreamRequestWithModel(
        string prompt,
        string model,
        bool useThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? userId = null,
        bool isSuperAdmin = false)
    {
        await foreach (var chunk in ExecuteOpenRouterStreamWithFallbackChain(prompt, useThinking, cancellationToken, model))
            yield return chunk;
    }

    private async IAsyncEnumerable<string> ExecuteOpenRouterStreamWithFallbackChain(
        string prompt,
        bool useThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? primaryModel = null)
    {
        if (string.IsNullOrWhiteSpace(_llmApiKey))
            throw new InternalServerErrorException("OpenRouter API key is not configured.");

        var modelsToTry = new List<string>();
        if (!string.IsNullOrWhiteSpace(primaryModel))
            modelsToTry.Add(primaryModel);

        foreach (var fb in _fallbackModels)
        {
            if (!modelsToTry.Contains(fb, StringComparer.OrdinalIgnoreCase))
                modelsToTry.Add(fb);
        }

        Exception? lastException = null;
        foreach (var model in modelsToTry)
        {
            var channel = Channel.CreateUnbounded<string>();
            Exception? modelException = null;
            bool modelCompleted = false;
            bool anyChunksYielded = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteOpenRouterStreamRequest(prompt, useThinking, cancellationToken, model))
                        await channel.Writer.WriteAsync(chunk, cancellationToken);
                    modelCompleted = true;
                }
                catch (Exception ex)
                {
                    modelException = ex;
                    _logger.LogWarning(ex, "OpenRouter stream model {Model} failed. Trying next fallback...", model);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                anyChunksYielded = true;
                yield return chunk;
            }

            await producerTask;

            if (modelCompleted) yield break;

            // If partial content was already streamed, don't fall back — it would produce garbled output
            if (anyChunksYielded)
            {
                _logger.LogWarning("Model {Model} failed mid-stream after yielding partial content. Cannot fallback safely.", model);
                throw new InternalServerErrorException(
                    $"Model {model} failed mid-stream: {modelException?.Message}");
            }

            lastException = modelException;
        }

        throw new InternalServerErrorException(
            $"All OpenRouter stream models failed. Last error: {lastException?.Message}", lastException);
    }

    private async IAsyncEnumerable<string> ExecuteGeminiStreamRequestWithRotation(
        string prompt,
        bool useThinking,
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
                        var baseUrl = _geminiApiUrl.Replace(":generateContent", ":streamGenerateContent");
                        var url = $"{baseUrl}?key={currentKey}&alt=sse";// Use SSE for easier line-based parsing

                        var enableThinking = (_geminiIncludeThoughts || IsReasoningModel(_geminiModel)) && useThinking;

                        var requestBody = new
                        {
                            contents = new[] { new { parts = new[] { new { text = prompt } } } },
                            generationConfig = enableThinking ? new { thinking_config = new { include_thoughts = true } } : null
                        };

                        var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                        using var content = new StringContent(json, Encoding.UTF8, "application/json");

                        using var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Content = content;

                        // Send Model Name Protocol
                        await channel.Writer.WriteAsync($"__MODEL__:{_geminiModel}\n", cancellationToken);

                        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                        if (!response.IsSuccessStatusCode)
                        {
                            var error = await response.Content.ReadAsStringAsync(cancellationToken);
                            throw new Exception($"Gemini Stream Error {response.StatusCode}: {error}");
                        }

                        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        using var reader = new StreamReader(stream);

                        string? line;
                        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            if (!line.StartsWith("data: ")) continue;

                            var data = line[6..].Trim();
                            if (data == "[DONE]") break; // Only if Gemini sends this, usually standard SSE just ends

                            try
                            {
                                var chunk = JsonConvert.DeserializeObject<dynamic>(data);
                                if (chunk?.candidates != null && chunk.candidates.Count > 0)
                                {
                                    var parts = chunk.candidates[0].content?.parts;
                                    if (parts != null)
                                    {
                                        foreach (var part in parts)
                                        {
                                            // Handle Thought
                                            if (part.thought == true || part.thought != null)
                                            {
                                                var t = part.thought?.ToString();

                                                // If 'thought' is just a flag (true), get the actual text from 'text' property
                                                if ((t == "True" || string.IsNullOrEmpty(t)) && part.text != null)
                                                {
                                                    t = part.text.ToString();
                                                }

                                                // If we have content and it's not the boolean flag string representation
                                                if (!string.IsNullOrEmpty(t) && t != "True")
                                                {
                                                    await channel.Writer.WriteAsync($"__THINKING_CONTENT__:{t}\n", cancellationToken);
                                                }
                                                continue;
                                            }

                                            var text = part.text?.ToString();
                                            if (string.IsNullOrEmpty(text)) continue;

                                            // Process characters to handle Status Protocol (>>>)
                                            foreach (var c in text)
                                            {
                                                if (c == '\n')
                                                {
                                                    var bufferLine = lineBuffer.ToString();
                                                    lineBuffer.Clear();

                                                    if (bufferLine.TrimStart().StartsWith(">>>"))
                                                    {
                                                        var cleanLine = bufferLine.TrimStart()[3..].Trim();
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
                                    }
                                }
                            }
                            catch { }
                        }

                        // Flush remaining buffer
                        if (lineBuffer.Length > 0)
                        {
                            var bufferLine = lineBuffer.ToString();
                            if (bufferLine.TrimStart().StartsWith(">>>"))
                            {
                                await channel.Writer.WriteAsync($"__STATUS__:{bufferLine.TrimStart()[3..].Trim()}", cancellationToken);
                            }
                            else
                            {
                                await channel.Writer.WriteAsync(bufferLine, cancellationToken);
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
        bool useReasoning,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? modelOverride = null)
    {
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;

        var producerTask = Task.Run(async () =>
        {
            var lineBuffer = new StringBuilder();

            try
            {
                var model = modelOverride ?? _llmModel;
                var enableReasoning = (_llmIncludeReasoning || IsReasoningModel(model)) && useReasoning;

                var requestBody = new
                {
                    model,
                    messages = new[] { new { role = "user", content = prompt } },
                    stream = true,
                    include_reasoning = enableReasoning
                };

                var requestJson = JsonConvert.SerializeObject(requestBody);
                using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, _llmApiUrl);
                request.Content = requestContent;
                request.Headers.Add("Authorization", $"Bearer {_llmApiKey}");
                request.Headers.Add("HTTP-Referer", "https://bosla.me");
                request.Headers.Add("X-Title", "Bosla AI");

                _logger.LogInformation("Sending STREAM request to {Provider} with model {Model}", _llmProvider, model);

                // Send Model Name Protocol
                await channel.Writer.WriteAsync($"__MODEL__:{model}\n", cancellationToken);
                if (enableReasoning) await channel.Writer.WriteAsync($"__THINKING_CONTENT__: [Reasoning Enabled]\n", cancellationToken);

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

                    var jsonData = line[6..];
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

                        var content = chunk?.choices?[0]?.delta?.content?.ToString() ?? string.Empty;

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
                                    var cleanLine = bufferLine.TrimStart()[3..].Trim();
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
                        await channel.Writer.WriteAsync($"__STATUS__:{bufferLine.TrimStart()[3..].Trim()}", cancellationToken);
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

        // Route summarization to OpenRouter to save Gemini quota
        var (Response, _, _) = await SendRequestWithModel(prompt, _chatModel, useThinking: false);
        return Response;
    }
}
