using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Shared.DTOs;
using Shared.Enums;
using Shared.Options;

namespace Service.Helpers;

public class CustomerHelper
{
    private readonly ILogger<CustomerHelper> _logger;
    private readonly HttpClient _httpClient;
    private readonly UserRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<AiOptions> _options;

    // Gemini
    private readonly List<string> _geminiApiKeys;
    private readonly string _geminiModel;
    private readonly List<string> _geminiFallbackModels;
    private readonly string _geminiApiUrl;
    private readonly bool _geminiIncludeThoughts;
    private static int _currentGeminiKeyIndex = 0;

    // Cerebras (primary LLM)
    private readonly List<string> _llmApiKeys;
    private readonly string _llmApiUrl;
    private readonly string _llmModel;
    private readonly string _llmProvider;
    private static int _currentLlmKeyIndex = 0;

    // Groq (fallback LLM)
    private readonly List<string> _groqApiKeys;
    private readonly string _groqApiUrl;
    private readonly string _groqModel;
    private static int _currentGroqKeyIndex = 0;

    // Mistral (CV Analysis specialist)
    private readonly List<string> _mistralApiKeys;
    private readonly string _mistralApiUrl;
    private readonly string _mistralModel;
    private static int _currentMistralKeyIndex = 0;

    // Task-based model routing
    private readonly string _chatModel;
    private readonly string _reasoningModel;

    private static object[] BuildOpenAiMessages(string? systemPrompt, string userPrompt)
    {
        if (!string.IsNullOrEmpty(systemPrompt))
            return
            [
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            ];

        return [new { role = "user", content = userPrompt }];
    }

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
        _options = options;
        var aiOptions = options.CurrentValue;

        _geminiApiKeys = aiOptions.Gemini.ApiKeys;
        _geminiModel = aiOptions.Gemini.Model;
        _geminiFallbackModels = aiOptions.Gemini.FallbackModels;
        _geminiApiUrl = aiOptions.Gemini.ApiUrl;
        _geminiIncludeThoughts = aiOptions.Gemini.IncludeThoughts;

        _llmProvider = aiOptions.Llm.Provider;
        _llmApiKeys = aiOptions.Llm.ApiKeys;
        _llmApiUrl = aiOptions.Llm.ApiUrl;
        _llmModel = aiOptions.Llm.Model;

        _chatModel = aiOptions.Llm.ChatModel;
        _reasoningModel = aiOptions.Llm.ReasoningModel;

        _groqApiKeys = aiOptions.Groq.ApiKeys;
        _groqApiUrl = aiOptions.Groq.ApiUrl;
        _groqModel = aiOptions.Groq.Model;

        _mistralApiKeys = aiOptions.Mistral.ApiKeys;
        _mistralApiUrl = aiOptions.Mistral.ApiUrl;
        _mistralModel = aiOptions.Mistral.Model;

        if (!_geminiApiKeys.Any() && !_llmApiKeys.Any() && !_groqApiKeys.Any() && !_mistralApiKeys.Any())
        {
            _logger.LogWarning("No AI providers configured (Gemini, Cerebras, Groq, or Mistral). Service may fail.");
        }
    }

    public string GetModelForTask(LLMInteractionType taskType) => taskType switch
    {
        LLMInteractionType.ChatWithAI => _chatModel,
        LLMInteractionType.ChooseTrack => _chatModel,
        LLMInteractionType.TopicPreview => _chatModel,
        LLMInteractionType.RoadmapGeneration => _reasoningModel,
        LLMInteractionType.CVAnalysis => _reasoningModel,
        _ => _llmModel
    };

    public int GetGeminiRemainingQuota(string userId, bool isSuperAdmin)
        => _rateLimiter.GetRemainingRequests(userId, isSuperAdmin);

    public async Task<(string Response, string ModelName, string? ThinkingContent)> SendRequestToGemini(string prompt, bool useThinking = false, string? userId = null, bool isSuperAdmin = false)
    {
        if (_geminiApiKeys.Any())
        {
            if (userId != null && !_rateLimiter.TryConsumeRequest(userId, isSuperAdmin))
            {
                _logger.LogWarning("User {UserId} exceeded daily Gemini rate limit. Routing to {Provider}.", userId, _llmProvider);
            }
            else
            {
                try
                {
                    return await ExecuteGeminiRequestWithRotation(prompt, useThinking);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gemini Primary Provider Failed. Attempting Fallback to {Provider}...", _llmProvider);
                }
            }
        }
        else
        {
            _logger.LogWarning("Gemini keys not configured. Skipping to Fallback provider.");
        }

        // Fallback to Cerebras
        return await ExecuteLlmRequestWithKeyRotation(prompt, useThinking);
    }

    public async Task<(string Response, string ModelName, string? ThinkingContent)> SendRequestByTask(
        string prompt, LLMInteractionType taskType, bool useThinking = false,
        string? userId = null, bool isSuperAdmin = false, ChatMode chatMode = ChatMode.Normal)
    {
        var targetModel = GetModelForTask(taskType);

        // Powerful mode: route all tasks to Gemini (intent detection bypasses this via SendStreamRequestWithModel)
        if (chatMode == ChatMode.Powerful)
        {
            return await SendRequestToGemini(prompt, useThinking, userId, isSuperAdmin);
        }

        // Normal mode: CVAnalysis → Mistral (specialist) → Cerebras → Groq
        if (taskType == LLMInteractionType.CVAnalysis)
        {
            return await ExecuteMistralRequestWithKeyRotation(prompt, useThinking);
        }

        // Normal mode: Cerebras (primary) → Groq (fallback)
        return await ExecuteLlmRequestWithKeyRotation(prompt, useThinking, targetModel);
    }

    public async Task<(string Response, string ModelName, string? ThinkingContent)> SendRequestWithModel(
        string prompt, string model, bool useThinking = false, string? userId = null, bool isSuperAdmin = false)
    {
        return await ExecuteLlmRequestWithKeyRotation(prompt, useThinking, model);
    }

    // Cerebras: try all API keys with key rotation
    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteLlmRequestWithKeyRotation(
        string prompt, bool useThinking, string? modelOverride = null)
    {
        if (!_llmApiKeys.Any())
            throw new InternalServerErrorException($"{_llmProvider} API keys are not configured.");

        var model = modelOverride ?? _llmModel;

        for (int i = 0; i < _llmApiKeys.Count; i++)
        {
            var keyIndex = (_currentLlmKeyIndex + i) % _llmApiKeys.Count;
            var currentKey = _llmApiKeys[keyIndex];

            try
            {
                var result = await ExecuteLlmRequest(prompt, useThinking, model, currentKey);
                Interlocked.Exchange(ref _currentLlmKeyIndex, keyIndex);
                return result;
            }
            catch (Exception ex) when (IsRateLimitError(ex))
            {
                _logger.LogWarning("{Provider} Key {KeyIndex} rate limited. Trying next key...", _llmProvider, keyIndex + 1);
                continue;
            }
        }

        // All Cerebras keys exhausted, fall back to Groq
        _logger.LogWarning("All {Provider} API keys exhausted. Falling back to Groq...", _llmProvider);
        return await ExecuteGroqRequestWithKeyRotation(prompt, useThinking);
    }

    // Groq: try all API keys with key rotation
    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteGroqRequestWithKeyRotation(
        string prompt, bool useThinking)
    {
        if (!_groqApiKeys.Any())
            throw new InternalServerErrorException("All AI provider keys exhausted (Cerebras and Groq).");

        for (int i = 0; i < _groqApiKeys.Count; i++)
        {
            var keyIndex = (_currentGroqKeyIndex + i) % _groqApiKeys.Count;
            var currentKey = _groqApiKeys[keyIndex];

            try
            {
                var result = await ExecuteGroqRequest(prompt, useThinking, currentKey);
                Interlocked.Exchange(ref _currentGroqKeyIndex, keyIndex);
                return result;
            }
            catch (Exception ex) when (IsRateLimitError(ex))
            {
                _logger.LogWarning("Groq Key {KeyIndex} rate limited. Trying next key...", keyIndex + 1);
                continue;
            }
        }

        throw new InternalServerErrorException("All AI provider keys exhausted (Cerebras and Groq).");
    }

    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteGeminiRequestWithRotation(string prompt, bool useThinking)
    {
        // Try primary model first, then fallback models — each with all keys
        var modelsToTry = new List<string> { _geminiModel };
        modelsToTry.AddRange(_geminiFallbackModels.Where(m =>
            !string.IsNullOrWhiteSpace(m) &&
            !string.Equals(m, _geminiModel, StringComparison.OrdinalIgnoreCase)));

        foreach (var model in modelsToTry)
        {
            var apiUrl = _geminiApiUrl.Replace(_geminiModel, model);

            for (int i = 0; i < _geminiApiKeys.Count; i++)
            {
                var keyIndex = (_currentGeminiKeyIndex + i) % _geminiApiKeys.Count;
                var currentKey = _geminiApiKeys[keyIndex];

                _logger.LogInformation("Sending request to Gemini {Model} (Key {KeyIndex}/{TotalKeys})", model, keyIndex + 1, _geminiApiKeys.Count);

                try
                {
                    var enableThinking = (_geminiIncludeThoughts || IsReasoningModel(model)) && useThinking;

                    var requestBody = new
                    {
                        contents = new[] { new { parts = new[] { new { text = prompt } } } },
                        generationConfig = enableThinking ? new { thinking_config = new { include_thoughts = true } } : null
                    };

                    var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var geminiRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                    geminiRequest.Content = content;
                    geminiRequest.Headers.Add("x-goog-api-key", currentKey);

                    using var response = await _httpClient.SendAsync(geminiRequest);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Gemini Error {response.StatusCode}: {responseString}");
                    }

                    Interlocked.Exchange(ref _currentGeminiKeyIndex, keyIndex);

                    var text = ExtractTextFromResponse(responseString, out var thought);
                    return (text, model, thought);
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("quota"))
                {
                    _logger.LogWarning("Gemini Key {KeyIndex} rate limited. Trying next key...", keyIndex + 1);
                    continue;
                }
            }

            _logger.LogWarning("All Gemini keys exhausted for model {Model}. Trying fallback model...", model);
        }

        throw new InternalServerErrorException("All Gemini API keys and models exhausted.");
    }

    // Cerebras: single request with a specific key
    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteLlmRequest(
        string prompt, bool useThinking, string model, string apiKey)
    {
        var requestBody = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _llmApiUrl);
        request.Content = requestContent;
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        _logger.LogInformation("Sending request to {Provider} with model {Model}", _llmProvider, model);

        using var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException($"LLM API Error {response.StatusCode}: {responseContent}");
        }

        var text = ExtractTextFromOpenAIResponse(responseContent, out var reasoning);
        return (text, model, reasoning);
    }

    // Groq: single request with a specific key
    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteGroqRequest(
        string prompt, bool useThinking, string apiKey)
    {
        var requestBody = new { model = _groqModel, messages = new[] { new { role = "user", content = prompt } } };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _groqApiUrl);
        request.Content = requestContent;
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        _logger.LogInformation("Sending request to Groq with model {Model}", _groqModel);

        using var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException($"Groq API Error {response.StatusCode}: {responseContent}");
        }

        var text = ExtractTextFromOpenAIResponse(responseContent, out var reasoning);
        return (text, _groqModel, reasoning);
    }

    // Mistral: single request with a specific key (OpenAI-compatible)
    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteMistralRequest(
        string prompt, bool useThinking, string apiKey)
    {
        var requestBody = new { model = _mistralModel, messages = new[] { new { role = "user", content = prompt } } };

        var requestJson = JsonConvert.SerializeObject(requestBody);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _mistralApiUrl);
        request.Content = requestContent;
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        _logger.LogInformation("Sending request to Mistral with model {Model}", _mistralModel);

        using var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InternalServerErrorException($"Mistral API Error {response.StatusCode}: {responseContent}");
        }

        var text = ExtractTextFromOpenAIResponse(responseContent, out var reasoning);
        return (text, _mistralModel, reasoning);
    }

    // Mistral: try all API keys with key rotation, falls back to Cerebras→Groq cascade
    private async Task<(string Response, string ModelName, string? ThinkingContent)> ExecuteMistralRequestWithKeyRotation(
        string prompt, bool useThinking)
    {
        if (!_mistralApiKeys.Any())
        {
            _logger.LogWarning("Mistral API keys not configured. Falling back to {Provider}.", _llmProvider);
            return await ExecuteLlmRequestWithKeyRotation(prompt, useThinking);
        }

        for (int i = 0; i < _mistralApiKeys.Count; i++)
        {
            var keyIndex = (_currentMistralKeyIndex + i) % _mistralApiKeys.Count;
            var currentKey = _mistralApiKeys[keyIndex];

            try
            {
                var result = await ExecuteMistralRequest(prompt, useThinking, currentKey);
                Interlocked.Exchange(ref _currentMistralKeyIndex, keyIndex);
                return result;
            }
            catch (Exception ex) when (IsRateLimitError(ex))
            {
                _logger.LogWarning("Mistral Key {KeyIndex} rate limited. Trying next key...", keyIndex + 1);
                continue;
            }
        }

        // All Mistral keys exhausted, fall back to Cerebras→Groq cascade
        _logger.LogWarning("All Mistral API keys exhausted. Falling back to {Provider}...", _llmProvider);
        return await ExecuteLlmRequestWithKeyRotation(prompt, useThinking);
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
                    _logger.LogInformation("LLM Reasoning: {Reasoning}", reasoning);
                }
            }

            return message.content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse LLM JSON response");
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
                    if (part.thought != null && part.thought.ToString() != "False")
                    {
                        thought = part.text?.ToString() ?? part.thought.ToString();
                        _logger.LogDebug("Gemini Thought: {Length} chars", thought?.Length);
                        continue;
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

        _logger.LogError("Unknown JSON format or parsing error: {Preview}", responseJson?.Length > 500 ? responseJson[..500] + "..." : responseJson);
        throw new BadRequestException("Failed to parse AI response (Unknown format)");
    }

    public async IAsyncEnumerable<string> SendStreamRequestToGemini(
        string prompt,
        bool useThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? userId = null,
        bool isSuperAdmin = false,
        string? systemPrompt = null)
    {
        bool useLlmFallback = false;

        // Check per-user rate limit before Gemini
        bool rateLimited = userId != null && !_rateLimiter.TryConsumeRequest(userId, isSuperAdmin);
        if (rateLimited)
        {
            _logger.LogWarning("User {UserId} exceeded daily Gemini rate limit. Routing stream to {Provider}.", userId, _llmProvider);
            useLlmFallback = true;
        }

        if (!useLlmFallback && _geminiApiKeys.Any())
        {
            var channel = Channel.CreateUnbounded<string>();
            Exception? geminiException = null;
            bool geminiCompleted = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteGeminiStreamRequestWithRotation(prompt, useThinking, systemPrompt, cancellationToken))
                        await channel.Writer.WriteAsync(chunk, cancellationToken);
                    geminiCompleted = true;
                }
                catch (Exception ex)
                {
                    geminiException = ex;
                    _logger.LogWarning("Gemini streaming failed: {Error}. Will fallback to {Provider}...", ex.Message, _llmProvider);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            bool anyGeminiChunksYielded = false;
            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!chunk.StartsWith("__MODEL__:") && !chunk.StartsWith("__THINKING_CONTENT__:")
                    && !chunk.StartsWith("__STATUS__:") && !chunk.StartsWith("__FALLBACK__:"))
                    anyGeminiChunksYielded = true;
                yield return chunk;
            }

            await producerTask;

            if (geminiCompleted) yield break;

            // If partial content was already streamed, cannot safely fall back
            if (anyGeminiChunksYielded)
            {
                _logger.LogWarning("Gemini failed mid-stream after yielding content. Cannot fallback safely.");
                throw new InternalServerErrorException(
                    $"Gemini failed mid-stream: {geminiException?.Message}");
            }

            useLlmFallback = true;
            if (geminiException != null && !_groqApiKeys.Any() && !_llmApiKeys.Any())
                throw geminiException;
        }
        else if (!rateLimited)
        {
            useLlmFallback = true;
        }

        // Fallback to Groq streaming with key rotation
        if (useLlmFallback && _groqApiKeys.Any())
        {
            yield return $"__FALLBACK__:groq";
            await foreach (var chunk in ExecuteGroqStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt))
                yield return chunk;
        }
        else if (useLlmFallback && _llmApiKeys.Any())
        {
            yield return $"__FALLBACK__:{_llmProvider}";
            await foreach (var chunk in ExecuteLlmStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt: systemPrompt))
                yield return chunk;
        }
        else if (useLlmFallback)
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
        ChatMode chatMode = ChatMode.Normal,
        string? systemPrompt = null)
    {
        // Powerful mode: route all tasks to Gemini (intent detection bypasses this via SendStreamRequestWithModel)
        if (chatMode == ChatMode.Powerful)
        {
            await foreach (var chunk in SendStreamRequestToGemini(prompt, useThinking, cancellationToken, userId, isSuperAdmin, systemPrompt))
                yield return chunk;
            yield break;
        }

        // Normal mode: CVAnalysis → Mistral (specialist) → Cerebras → Groq
        if (taskType == LLMInteractionType.CVAnalysis)
        {
            await foreach (var chunk in ExecuteMistralStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt))
                yield return chunk;
            yield break;
        }

        // Normal mode: Cerebras (primary) → Groq (fallback)
        await foreach (var chunk in ExecuteLlmStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt: systemPrompt))
            yield return chunk;
    }

    public async IAsyncEnumerable<string> SendStreamRequestWithModel(
        string prompt,
        string model,
        bool useThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? userId = null,
        bool isSuperAdmin = false,
        string? systemPrompt = null)
    {
        await foreach (var chunk in ExecuteLlmStreamWithKeyRotation(prompt, useThinking, cancellationToken, model, systemPrompt))
            yield return chunk;
    }

    // Streams from Cerebras with key rotation — tries each API key on rate limit
    private async IAsyncEnumerable<string> ExecuteLlmStreamWithKeyRotation(
        string prompt,
        bool useThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? modelOverride = null,
        string? systemPrompt = null)
    {
        if (!_llmApiKeys.Any())
            throw new InternalServerErrorException($"{_llmProvider} API keys are not configured.");

        Exception? lastException = null;
        var startLlmIndex = Volatile.Read(ref _currentLlmKeyIndex);
        for (int i = 0; i < _llmApiKeys.Count; i++)
        {
            var keyIndex = (startLlmIndex + i) % _llmApiKeys.Count;
            var currentKey = _llmApiKeys[keyIndex];

            var channel = Channel.CreateUnbounded<string>();
            Exception? keyException = null;
            bool keyCompleted = false;
            bool anyChunksYielded = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteLlmStreamRequest(prompt, useThinking, currentKey, cancellationToken, modelOverride, systemPrompt))
                        await channel.Writer.WriteAsync(chunk, cancellationToken);
                    keyCompleted = true;
                }
                catch (Exception ex)
                {
                    keyException = ex;
                    _logger.LogWarning(ex, "{Provider} stream key {KeyIndex} failed. Trying next...", _llmProvider, keyIndex + 1);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!chunk.StartsWith("__MODEL__:") && !chunk.StartsWith("__THINKING_CONTENT__:")
                    && !chunk.StartsWith("__STATUS__:") && !chunk.StartsWith("__FALLBACK__:"))
                    anyChunksYielded = true;
                yield return chunk;
            }

            await producerTask;

            if (keyCompleted)
            {
                Interlocked.Exchange(ref _currentLlmKeyIndex, keyIndex);
                yield break;
            }

            if (anyChunksYielded)
            {
                if (keyException != null && IsRetryableStreamError(keyException))
                {
                    _logger.LogWarning("{Provider} key {KeyIndex} failed with a retryable mid-stream error. Will retry with next key...", _llmProvider, keyIndex + 1);
                    yield return "__RETRY__";
                    lastException = keyException;
                    continue;
                }
                _logger.LogWarning("{Provider} key {KeyIndex} failed mid-stream after yielding content.", _llmProvider, keyIndex + 1);
                throw new InternalServerErrorException(
                    $"{_llmProvider} failed mid-stream: {keyException?.Message}");
            }

            // Only retry on explicitly retryable stream errors
            if (keyException != null && !IsRetryableStreamError(keyException))
                throw keyException;

            lastException = keyException;
        }

        // All Cerebras keys exhausted, fall back to Groq streaming
        _logger.LogWarning("All {Provider} stream keys exhausted. Falling back to Groq...", _llmProvider);
        yield return $"__FALLBACK__:groq";
        await foreach (var chunk in ExecuteGroqStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt))
            yield return chunk;
    }

    // Streams from Groq with key rotation — final fallback, no further cascade
    private async IAsyncEnumerable<string> ExecuteGroqStreamWithKeyRotation(
        string prompt,
        bool useThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? systemPrompt = null)
    {
        if (!_groqApiKeys.Any())
            throw new InternalServerErrorException("All AI provider keys exhausted (Cerebras and Groq).");

        Exception? lastException = null;
        var startGroqIndex = Volatile.Read(ref _currentGroqKeyIndex);
        for (int i = 0; i < _groqApiKeys.Count; i++)
        {
            var keyIndex = (startGroqIndex + i) % _groqApiKeys.Count;
            var currentKey = _groqApiKeys[keyIndex];

            var channel = Channel.CreateUnbounded<string>();
            Exception? keyException = null;
            bool keyCompleted = false;
            bool anyChunksYielded = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteGroqStreamRequest(prompt, useThinking, currentKey, cancellationToken, systemPrompt))
                        await channel.Writer.WriteAsync(chunk, cancellationToken);
                    keyCompleted = true;
                }
                catch (Exception ex)
                {
                    keyException = ex;
                    _logger.LogWarning(ex, "Groq stream key {KeyIndex} failed. Trying next...", keyIndex + 1);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!chunk.StartsWith("__MODEL__:") && !chunk.StartsWith("__THINKING_CONTENT__:")
                    && !chunk.StartsWith("__STATUS__:") && !chunk.StartsWith("__FALLBACK__:"))
                    anyChunksYielded = true;
                yield return chunk;
            }

            await producerTask;

            if (keyCompleted)
            {
                Interlocked.Exchange(ref _currentGroqKeyIndex, keyIndex);
                yield break;
            }

            if (anyChunksYielded)
            {
                if (keyException != null && IsRetryableStreamError(keyException))
                {
                    _logger.LogWarning("Groq key {KeyIndex} failed with a retryable mid-stream error. Will retry with next key...", keyIndex + 1);
                    yield return "__RETRY__";
                    lastException = keyException;
                    continue;
                }
                _logger.LogWarning("Groq key {KeyIndex} failed mid-stream after yielding content.", keyIndex + 1);
                throw new InternalServerErrorException(
                    $"Groq failed mid-stream: {keyException?.Message}");
            }

            if (keyException != null && !IsRetryableStreamError(keyException))
                throw keyException;

            lastException = keyException;
        }

        // All Groq keys exhausted — no further fallback
        throw new InternalServerErrorException(
            $"All AI provider stream keys exhausted (Cerebras and Groq). Last error: {lastException?.Message}", lastException);
    }

    // Streams from Mistral with key rotation — falls back to Cerebras→Groq cascade
    private async IAsyncEnumerable<string> ExecuteMistralStreamWithKeyRotation(
        string prompt,
        bool useThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? systemPrompt = null)
    {
        if (!_mistralApiKeys.Any())
        {
            _logger.LogWarning("Mistral stream keys not configured. Falling back to {Provider}...", _llmProvider);
            yield return $"__FALLBACK__:{_llmProvider}";
            await foreach (var chunk in ExecuteLlmStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt: systemPrompt))
                yield return chunk;
            yield break;
        }

        Exception? lastException = null;
        var startMistralIndex = Volatile.Read(ref _currentMistralKeyIndex);
        for (int i = 0; i < _mistralApiKeys.Count; i++)
        {
            var keyIndex = (startMistralIndex + i) % _mistralApiKeys.Count;
            var currentKey = _mistralApiKeys[keyIndex];

            var channel = Channel.CreateUnbounded<string>();
            Exception? keyException = null;
            bool keyCompleted = false;
            bool anyChunksYielded = false;

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var chunk in ExecuteMistralStreamRequest(prompt, useThinking, currentKey, systemPrompt, cancellationToken))
                        await channel.Writer.WriteAsync(chunk, cancellationToken);
                    keyCompleted = true;
                }
                catch (Exception ex)
                {
                    keyException = ex;
                    _logger.LogWarning(ex, "Mistral stream key {KeyIndex} failed. Trying next...", keyIndex + 1);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (!chunk.StartsWith("__MODEL__:") && !chunk.StartsWith("__THINKING_CONTENT__:")
                    && !chunk.StartsWith("__STATUS__:") && !chunk.StartsWith("__FALLBACK__:"))
                    anyChunksYielded = true;
                yield return chunk;
            }

            await producerTask;

            if (keyCompleted)
            {
                Interlocked.Exchange(ref _currentMistralKeyIndex, keyIndex);
                yield break;
            }

            if (anyChunksYielded)
            {
                if (keyException != null && IsRetryableStreamError(keyException))
                {
                    _logger.LogWarning("Mistral key {KeyIndex} failed with a retryable mid-stream error. Will retry with next key...", keyIndex + 1);
                    yield return "__RETRY__";
                    lastException = keyException;
                    continue;
                }
                _logger.LogWarning("Mistral key {KeyIndex} failed mid-stream after yielding content.", keyIndex + 1);
                throw new InternalServerErrorException(
                    $"Mistral failed mid-stream: {keyException?.Message}");
            }

            if (keyException != null && !IsRetryableStreamError(keyException))
                throw keyException;

            lastException = keyException;
        }

        // All Mistral keys exhausted, fall back to Cerebras→Groq cascade
        _logger.LogWarning("All Mistral stream keys exhausted. Falling back to {Provider}...", _llmProvider);
        yield return $"__FALLBACK__:{_llmProvider}";
        await foreach (var chunk in ExecuteLlmStreamWithKeyRotation(prompt, useThinking, cancellationToken, systemPrompt: systemPrompt))
            yield return chunk;
    }

    // Streams from Mistral (OpenAI-compatible SSE)
    private async IAsyncEnumerable<string> ExecuteMistralStreamRequest(
        string prompt,
        bool useThinking,
        string apiKey,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;

        var producerTask = Task.Run(async () =>
        {
            var lineBuffer = new StringBuilder();

            try
            {
                var messages = BuildOpenAiMessages(systemPrompt, prompt);
                var requestBody = new { model = _mistralModel, messages, stream = true };

                var requestJson = JsonConvert.SerializeObject(requestBody);
                using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, _mistralApiUrl);
                request.Content = requestContent;
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                _logger.LogInformation("Sending STREAM request to Mistral with model {Model}", _mistralModel);

                await channel.Writer.WriteAsync($"__MODEL__:{_mistralModel}\n", cancellationToken);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new InternalServerErrorException($"Mistral Stream Error {response.StatusCode}: {errorContent}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                await ProcessOpenAiSseStream(reader, channel.Writer, lineBuffer, "Mistral", cancellationToken);
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

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            yield return chunk;

        await producerTask;

        if (streamException != null)
            throw streamException;
    }

    private async IAsyncEnumerable<string> ExecuteGeminiStreamRequestWithRotation(
        string prompt,
        bool useThinking,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;
        var lineBuffer = new StringBuilder();

        // Try primary model, then fallback models — each with all keys
        var modelsToTry = new List<string> { _geminiModel };
        modelsToTry.AddRange(_geminiFallbackModels.Where(m =>
            !string.IsNullOrWhiteSpace(m) &&
            !string.Equals(m, _geminiModel, StringComparison.OrdinalIgnoreCase)));

        var producerTask = Task.Run(async () =>
        {
            try
            {
                foreach (var model in modelsToTry)
                {
                    for (int i = 0; i < _geminiApiKeys.Count; i++)
                    {
                        var keyIndex = (_currentGeminiKeyIndex + i) % _geminiApiKeys.Count;
                        var currentKey = _geminiApiKeys[keyIndex];

                        _logger.LogInformation("Sending STREAM request to Gemini model {Model} (Key {KeyIndex}/{TotalKeys})",
                            model, keyIndex + 1, _geminiApiKeys.Count);

                        try
                        {
                            // Build URL with current model
                            var modelUrl = _geminiApiUrl.Replace(_geminiModel, model);
                            var baseUrl = modelUrl.Replace(":generateContent", ":streamGenerateContent");
                            var url = $"{baseUrl}?alt=sse";

                            var enableThinking = (_geminiIncludeThoughts || IsReasoningModel(model)) && useThinking;

                            object? systemInstruction = !string.IsNullOrWhiteSpace(systemPrompt)
                                ? new { parts = new[] { new { text = systemPrompt } } }
                                : null;

                            var requestBody = new
                            {
                                system_instruction = systemInstruction,
                                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                                generationConfig = enableThinking ? new { thinking_config = new { include_thoughts = true } } : null
                            };

                            var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                            using var content = new StringContent(json, Encoding.UTF8, "application/json");

                            using var request = new HttpRequestMessage(HttpMethod.Post, url);
                            request.Headers.Add("x-goog-api-key", currentKey);
                            request.Content = content;

                            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                            if (!response.IsSuccessStatusCode)
                            {
                                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                                throw new Exception($"Gemini Stream Error {response.StatusCode}: {error}");
                            }

                            await channel.Writer.WriteAsync($"__MODEL__:{model}\n", cancellationToken);

                            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                            using var reader = new StreamReader(stream);

                            string? line;
                            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                if (!line.StartsWith("data: ")) continue;

                                var data = line[6..].Trim();
                                if (data == "[DONE]") break;

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
                                                if (part.thought != null && part.thought.ToString() != "False")
                                                {
                                                    var t = part.thought?.ToString();
                                                    if ((t == "True" || string.IsNullOrEmpty(t)) && part.text != null)
                                                        t = part.text.ToString();
                                                    if (!string.IsNullOrEmpty(t) && t != "True")
                                                        await channel.Writer.WriteAsync($"__THINKING_CONTENT__:{t}\n", cancellationToken);
                                                    continue;
                                                }

                                                var text = part.text?.ToString();
                                                if (string.IsNullOrEmpty(text)) continue;

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
                                    await channel.Writer.WriteAsync($"__STATUS__:{bufferLine.TrimStart()[3..].Trim()}", cancellationToken);
                                else
                                    await channel.Writer.WriteAsync(bufferLine, cancellationToken);
                            }

                            Interlocked.Exchange(ref _currentGeminiKeyIndex, keyIndex);
                            return; // Success
                        }
                        catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("RESOURCE_EXHAUSTED") || ex.Message.Contains("quota"))
                        {
                            _logger.LogWarning("Gemini Stream model {Model} Key {KeyIndex} rate limited. Switching...", model, keyIndex + 1);
                            lineBuffer.Clear();
                            continue;
                        }
                    }

                    _logger.LogWarning("All Gemini keys exhausted for model {Model}. Trying fallback model...", model);
                }

                streamException = new InternalServerErrorException("All Gemini models and keys failed or were rate-limited.");
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

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            yield return chunk;

        await producerTask;

        if (streamException != null)
            throw streamException;
    }

    // Streams from Cerebras/LLM provider (OpenAI-compatible SSE)
    private async IAsyncEnumerable<string> ExecuteLlmStreamRequest(
        string prompt,
        bool useThinking,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? modelOverride = null,
        string? systemPrompt = null)
    {
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;

        var producerTask = Task.Run(async () =>
        {
            var lineBuffer = new StringBuilder();

            try
            {
                var model = modelOverride ?? _llmModel;

                var messages = BuildOpenAiMessages(systemPrompt, prompt);
                var requestBody = new
                {
                    model,
                    messages,
                    stream = true
                };

                var requestJson = JsonConvert.SerializeObject(requestBody);
                using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, _llmApiUrl);
                request.Content = requestContent;
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                _logger.LogInformation("Sending STREAM request to {Provider} with model {Model}", _llmProvider, model);

                await channel.Writer.WriteAsync($"__MODEL__:{model}\n", cancellationToken);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new InternalServerErrorException($"{_llmProvider} Stream Error {response.StatusCode}: {errorContent}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                await ProcessOpenAiSseStream(reader, channel.Writer, lineBuffer, _llmProvider, cancellationToken);
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

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            yield return chunk;

        await producerTask;

        if (streamException != null)
            throw streamException;
    }

    // Streams from Groq (OpenAI-compatible SSE)
    private async IAsyncEnumerable<string> ExecuteGroqStreamRequest(
        string prompt,
        bool useThinking,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        string? systemPrompt = null)
    {
        var channel = Channel.CreateUnbounded<string>();
        Exception? streamException = null;

        var producerTask = Task.Run(async () =>
        {
            var lineBuffer = new StringBuilder();

            try
            {
                var messages = BuildOpenAiMessages(systemPrompt, prompt);
                var requestBody = new { model = _groqModel, messages, stream = true };

                var requestJson = JsonConvert.SerializeObject(requestBody);
                using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var request = new HttpRequestMessage(HttpMethod.Post, _groqApiUrl);
                request.Content = requestContent;
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                _logger.LogInformation("Sending STREAM request to Groq with model {Model}", _groqModel);

                await channel.Writer.WriteAsync($"__MODEL__:{_groqModel}\n", cancellationToken);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new InternalServerErrorException($"Groq Stream Error {response.StatusCode}: {errorContent}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                await ProcessOpenAiSseStream(reader, channel.Writer, lineBuffer, "Groq", cancellationToken);
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

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            yield return chunk;

        await producerTask;

        if (streamException != null)
            throw streamException;
    }

    // Shared SSE stream processor for OpenAI-compatible APIs (Cerebras, Groq)
    private static async Task ProcessOpenAiSseStream(
        StreamReader reader,
        ChannelWriter<string> writer,
        StringBuilder lineBuffer,
        string providerName,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith(":")) continue;
            if (!line.StartsWith("data: ")) continue;

            var jsonData = line[6..];
            if (jsonData == "[DONE]") break;

            try
            {
                var chunk = JsonConvert.DeserializeObject<dynamic>(jsonData);

                if (chunk?.error != null)
                {
                    string errorMessage = chunk.error.message?.ToString() ?? "Unknown error";
                    throw new InternalServerErrorException($"{providerName} stream error: {errorMessage}");
                }

                var choices = chunk?.choices;
                if (choices == null || choices.Count == 0) continue;

                // Extract reasoning/thinking content (e.g. Groq sends delta.reasoning)
                var reasoning = (string)(choices[0]?.delta?.reasoning?.ToString() ?? string.Empty);
                if (!string.IsNullOrEmpty(reasoning))
                    await writer.WriteAsync($"__THINKING_CONTENT__:{reasoning}", cancellationToken);

                var content = (string)(choices[0]?.delta?.content?.ToString() ?? string.Empty);
                if (string.IsNullOrEmpty(content)) continue;

                foreach (var c in content)
                {
                    if (c == '\n')
                    {
                        var bufferLine = lineBuffer.ToString();
                        lineBuffer.Clear();

                        if (bufferLine.TrimStart().StartsWith(">>>"))
                        {
                            var cleanLine = bufferLine.TrimStart()[3..].Trim();
                            await writer.WriteAsync($"__STATUS__:{cleanLine}", cancellationToken);
                        }
                        else
                        {
                            await writer.WriteAsync(bufferLine + "\n", cancellationToken);
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
                await writer.WriteAsync($"__STATUS__:{bufferLine.TrimStart()[3..].Trim()}", cancellationToken);
            else
                await writer.WriteAsync(bufferLine, cancellationToken);
        }
    }

    private static bool IsRateLimitError(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("429") || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("quota", StringComparison.OrdinalIgnoreCase) || msg.Contains("RESOURCE_EXHAUSTED")
            || msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("queue_exceeded", StringComparison.OrdinalIgnoreCase)
            // Also fallback on model availability errors (e.g., model removed or no access)
            || msg.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("not_found_error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetryableStreamError(Exception ex)
    {
        if (ex is OperationCanceledException)
            return false;

        if (IsRateLimitError(ex))
            return true;

        var msg = ex.Message;
        return msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("http2", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("protocol error", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("connection closed", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("stream ended", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("end of stream", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("service unavailable", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("bad gateway", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("gateway timeout", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("502")
               || msg.Contains("503")
               || msg.Contains("504");
    }

    public async Task<string> CompactConversationAsync(string conversationHistory)
    {
        var template = _options.CurrentValue.Prompts.SummarizationPromptTemplate;

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Compact the following conversation into a short, high-signal context memory. Keep only enduring facts (goals, preferences, constraints, decisions, pending actions) and remove repetition/chit-chat. Keep it under 200 words.\\n\\nConversation:\\n{0}\\n\\nOutput format:\\n- **User Goals**: what they want to achieve\\n- **Preferences & Constraints**: language, budget, platforms, boundaries\\n- **Decisions Made**: choices already confirmed\\n- **Pending Actions**: next concrete steps\\n\\nReturn compact context:";
        }

        var prompt = string.Format(template, conversationHistory);

        // Route compaction to LLM provider to save Gemini quota
        var (Response, _, _) = await SendRequestWithModel(prompt, _chatModel, useThinking: false);
        return Response;
    }

    public Task<string> SummarizeConversationAsync(string conversationHistory)
        => CompactConversationAsync(conversationHistory);

    public async Task<string> ClassifyModeAsync(string query, int sessionMessageCount, bool profileComplete)
    {
        var template = _options.CurrentValue.Prompts.ModeClassificationPrompt;

        if (string.IsNullOrWhiteSpace(template))
        {
            // Fallback if prompt not configured
            return "ACTION";
        }

        var profileStatus = profileComplete ? "complete" : "incomplete";
        var prompt = string.Format(template, query, sessionMessageCount, profileStatus);

        try
        {
            // Use lightweight call - no thinking, fast model
            var (response, _, _) = await SendRequestWithModel(prompt, _chatModel, useThinking: false);

            var normalized = response.Trim().ToUpperInvariant();

            // Extract just the mode word
            if (normalized.Contains("FRIEND")) return "FRIEND";
            if (normalized.Contains("ACTION")) return "ACTION";
            if (normalized.Contains("UNCLEAR")) return "UNCLEAR";

            // Default to ACTION if parsing fails (fallback to current behavior)
            _logger.LogWarning("Mode classification returned unexpected value: {Response}. Defaulting to ACTION.", response);
            return "ACTION";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mode classification failed. Defaulting to ACTION.");
            return "ACTION";
        }
    }

    public async Task<(LLMInteractionType Intent, float Confidence, string? TargetRole)> ClassifyIntentSimplifiedAsync(
        string query, string? profileSummary)
    {
        var template = _options.CurrentValue.Prompts.SimplifiedIntentPrompt;

        if (string.IsNullOrWhiteSpace(template))
        {
            // Fallback if prompt not configured
            return (LLMInteractionType.ChatWithAI, 50, null);
        }

        var prompt = string.Format(template, query, profileSummary ?? "No profile data available");

        try
        {
            var (response, _, _) = await SendRequestWithModel(prompt, _chatModel, useThinking: false);

            // Parse the JSON response
            var cleanJson = response.Trim();
            if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Replace("```json", "").Replace("```", "").Trim();
            }

            var parsed = JsonConvert.DeserializeAnonymousType(cleanJson, new
            {
                intent = "",
                confidence = 0f,
                target_role = (string?)null
            });

            if (parsed == null)
            {
                return (LLMInteractionType.ChatWithAI, 50, null);
            }

            var intent = parsed.intent?.ToLowerInvariant() switch
            {
                "roadmapgeneration" => LLMInteractionType.RoadmapGeneration,
                "cvanalysis" => LLMInteractionType.CVAnalysis,
                "topicpreview" => LLMInteractionType.TopicPreview,
                "choosetrack" => LLMInteractionType.ChooseTrack,
                _ => LLMInteractionType.ChatWithAI
            };

            return (intent, parsed.confidence, parsed.target_role);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Simplified intent classification failed. Defaulting to ChatWithAI.");
            return (LLMInteractionType.ChatWithAI, 50, null);
        }
    }

    public async Task<UserProfileExtraction?> ExtractProfileAsync(string conversationHistory)
    {
        var template = _options.CurrentValue.Prompts.ProfileExtractionPrompt;

        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var prompt = string.Format(template, conversationHistory);

        try
        {
            var (response, _, _) = await SendRequestWithModel(prompt, _chatModel, useThinking: false);

            var cleanJson = response.Trim();
            if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Replace("```json", "").Replace("```", "").Trim();
            }

            return JsonConvert.DeserializeObject<UserProfileExtraction>(cleanJson);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Profile extraction failed.");
            return null;
        }
    }
}
