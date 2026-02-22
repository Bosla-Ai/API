using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications;
using Domain.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Service.Helpers;
using Shared;
using Shared.DTOs;
using Shared.DTOs.CustomerDTOs;
using Shared.DTOs.RoadmapDTOs;
using Shared.Enums;
using Shared.Options;

namespace Service.Implementations;

public class CustomerService(
    IUnitOfWork unitOfWork
    , IMapper mapper
    , ILogger<CustomerService> _logger
    , CustomerHelper customerHelper
    , ConversationContextManager conversationContextManager
    , IOptionsMonitor<AiOptions> options
    , IHttpClientFactory httpClientFactory
    , AiRequestStore aiRequestStore
    , IJobMarketService jobMarketService) : ICustomerService
{
    public async Task<APIResponse<string>> ProcessUserQueryAsync(string userId, string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("User ID is required");
        if (string.IsNullOrWhiteSpace(query))
            throw new BadRequestException("Query is empty");

        var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");

        var conversationContext = await conversationContextManager.GetConversationContextAsync(userId, actualSessionId);

        string aiPrompt = $"Context:\n{conversationContext}\n\nCurrent User Query: {query}\n\nPlease provide a helpful response considering the conversation history if relevant.";

        var (geminiResponse, _, _) = await customerHelper.SendRequestToGemini(aiPrompt, useThinking: false);
        string responseText = geminiResponse;

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, responseText, "assistant");

        return new APIResponse<string>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = responseText
        };
    }

    public async IAsyncEnumerable<string> ProcessUserQueryStreamAsync(string userId, string query, string? sessionId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("User ID is required");
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

        var pendingSseEvents = new ConcurrentQueue<string>();
        conversationContextManager.OnSseEvent = (eventName, data) =>
        {
            pendingSseEvents.Enqueue(FormatSse(eventName, data));
        };

        var contextTask = conversationContextManager.GetConversationContextAsync(userId, actualSessionId);
        var addMessageTask = conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");
        await Task.WhenAll(contextTask, addMessageTask);
        var conversationContext = await contextTask;

        // Drain any buffered SSE events (e.g. summarization)
        while (pendingSseEvents.TryDequeue(out var sseEvent))
            yield return sseEvent;

        yield return FormatSse("status", new { message = "Analyzing your request...", step = "init" });

        string marketContext = "";
        MarketInsightDTO? fetchedMarketInsight = null;
        var marketKeywords = ExtractKeywordsFromQuery(query);
        if (marketKeywords.Length > 0)
        {
            yield return FormatSse("tool", new { name = "MarketAnalysis", state = "start", summary = $"Scanning labor market for: {string.Join(", ", marketKeywords)}..." });

            var pendingEvents = new List<string>();
            try
            {
                fetchedMarketInsight = await jobMarketService.GetMarketInsightsAsync(marketKeywords);
                cancellationToken.ThrowIfCancellationRequested();
                if (fetchedMarketInsight is not null)
                {
                    marketContext = fetchedMarketInsight.ToPromptContext();
                    var topSkillsSummary = string.Join(", ", fetchedMarketInsight.TopRequiredSkills.Take(5));
                    var salaryInfo = fetchedMarketInsight.Salary is not null
                        ? $" | Salary range: {fetchedMarketInsight.Salary.Currency}{fetchedMarketInsight.Salary.Min:N0}–{fetchedMarketInsight.Salary.Currency}{fetchedMarketInsight.Salary.Max:N0}"
                        : "";
                    pendingEvents.Add(FormatSse("tool", new
                    {
                        name = "MarketAnalysis",
                        state = "end",
                        summary = $"Analyzed {fetchedMarketInsight.TotalJobsAnalyzed} jobs. Top skills: {topSkillsSummary}{salaryInfo}"
                    }));

                    var pulse = jobMarketService.CalculateReadiness(marketKeywords, fetchedMarketInsight);
                    if (pulse is not null)
                    {
                        pendingEvents.Add(FormatSse("career_pulse", new
                        {
                            readinessScore = pulse.ReadinessScore,
                            readinessLevel = pulse.ReadinessLevel,
                            matchedSkills = pulse.MatchedSkills,
                            topGaps = pulse.TopGaps.Select(g => new { skill = g.Skill, demandPercent = g.DemandPercent, category = g.Category }),
                            insight = pulse.Insight,
                            targetRole = pulse.TargetRole,
                            jobsAnalyzed = pulse.JobsAnalyzed
                        }));
                    }
                }
                else
                {
                    pendingEvents.Add(FormatSse("tool", new { name = "MarketAnalysis", state = "end", summary = "No market data available — using knowledge base." }));
                }
            }
            catch
            {
                pendingEvents.Add(FormatSse("tool", new { name = "MarketAnalysis", state = "end", summary = "Market search skipped — using knowledge base." }));
            }

            foreach (var evt in pendingEvents)
                yield return evt;
        }

        // Intent Detection
        var prompts = options.CurrentValue.Prompts;

        // Inject market context into system prompt
        var systemPrompt = prompts.IntentDetectionSystemPrompt;
        if (!string.IsNullOrEmpty(marketContext))
        {
            systemPrompt = systemPrompt.Replace("{MARKET_CONTEXT}", marketContext);
        }
        else
        {
            systemPrompt = systemPrompt.Replace("{MARKET_CONTEXT}",
                "No real-time market data available. Use your knowledge of current labor market trends.");
        }

        var detectionPrompt = string.Format(prompts.IntentDetectionUserPromptTemplate,
            systemPrompt,
            (!string.IsNullOrEmpty(conversationContext.ToString()) ? $"Conversation History:\n{conversationContext}\n\n" : ""),
            query);

        var detectionJsonBuilder = new StringBuilder();
        string detectionModel = "Unknown";
        string thinkingLog = "";
        string? thinkingTitle = null;
        bool titleExtracted = false;

        // Stream detection to catch thoughts in real-time
        await foreach (var chunk in customerHelper.SendStreamRequestToGemini(detectionPrompt, useThinking: true, cancellationToken: cancellationToken))
        {
            if (chunk.StartsWith("__FALLBACK__:"))
            {
                var provider = chunk[13..].Trim();
                yield return FormatSse("fallback", new { provider, message = "Switched to backup AI model" });
            }
            else if (chunk.StartsWith("__MODEL__:"))
            {
                detectionModel = chunk[10..].Trim();
                yield return FormatSse("model", new { name = detectionModel });
            }
            else if (chunk.StartsWith("__THINKING_CONTENT__:"))
            {
                var content = chunk[21..];
                thinkingLog += content;

                // Title Extraction Logic: First line is title, rest is debug log
                if (!titleExtracted)
                {
                    if (thinkingLog.Contains("\n"))
                    {
                        var firstNewLineIndex = thinkingLog.IndexOf('\n');
                        thinkingTitle = thinkingLog[..firstNewLineIndex].Trim('*', ' ', '#'); // Clean md formatting
                        titleExtracted = true;

                        // Emit title
                        yield return FormatSse("thinking", new { title = thinkingTitle, debug = "" });

                        // Emit remainder as debug
                        var remainder = thinkingLog[(firstNewLineIndex + 1)..];
                        if (!string.IsNullOrEmpty(remainder))
                        {
                            yield return FormatSse("thinking", new { title = thinkingTitle, debug = remainder });
                        }
                    }
                }
                else
                {
                    // Already extracted title, stream as debug delta
                    yield return FormatSse("thinking", new { title = thinkingTitle ?? "Thinking...", debug = content });
                }
            }
            else if (!chunk.StartsWith("__STATUS__"))
            {
                detectionJsonBuilder.Append(chunk);
            }
        }

        // Parse the accumulated JSON
        var responseText = detectionJsonBuilder.ToString();
        var parsed = ParseCombinedResponse(responseText);

        var interactionType = parsed?.intent ?? LLMInteractionType.ChatWithAI;
        var confidence = parsed?.confidence ?? 0;
        var aiResponse = parsed?.response;
        var toolArguments = parsed?.toolArguments;
        var targetRole = parsed?.targetRole;
        var followUpSuggestions = parsed?.followUpSuggestions;
        var videoUrl = parsed?.videoUrl;
        var videoSearchQuery = parsed?.videoSearchQuery;

        // If we didn't get market data yet but now have a target role, try again
        if (fetchedMarketInsight is null && !string.IsNullOrEmpty(targetRole))
        {
            string? retryMarketEvent = null;
            try
            {
                var roleKeywords = new[] { targetRole }.Concat(marketKeywords).Distinct().Take(5).ToArray();
                fetchedMarketInsight = await jobMarketService.GetMarketInsightsAsync(roleKeywords);
                cancellationToken.ThrowIfCancellationRequested();
                if (fetchedMarketInsight is not null)
                {
                    marketContext = fetchedMarketInsight.ToPromptContext();
                    var topSkillsSummary = string.Join(", ", fetchedMarketInsight.TopRequiredSkills.Take(5));
                    retryMarketEvent = FormatSse("tool", new
                    {
                        name = "MarketAnalysis",
                        state = "end",
                        summary = $"Analyzed {fetchedMarketInsight.TotalJobsAnalyzed} jobs for {targetRole}. Top skills: {topSkillsSummary}"
                    });
                }
            }
            catch { /* market data is optional */ }

            if (retryMarketEvent is not null)
                yield return retryMarketEvent;
        }

        // --- End Intent Detection ---

        yield return FormatSse("intent", new
        {
            name = interactionType.ToString(),
            confidence,
            tags = toolArguments?.Tags,
            targetRole,
        });

        if (interactionType == LLMInteractionType.TopicPreview)
        {
            var previewMessage = !string.IsNullOrEmpty(aiResponse) ? aiResponse : "Here's a quick look at this topic:";
            var searchTerm = videoSearchQuery ?? videoUrl ?? query;

            yield return FormatSse("tool", new { name = "VideoSearch", state = "start", summary = $"Searching embeddable videos for: {searchTerm}..." });

            string? resolvedVideoUrl = null;
            string? videoTitle = null;
            try
            {
                var pipelineVideoSearchUrl = options.CurrentValue.PipelineApi.VideoSearchUrl;
                using var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(5);

                var encodedQuery = Uri.EscapeDataString(searchTerm);
                var url = $"{pipelineVideoSearchUrl}?q={encodedQuery}&lang=en";
                var response = await client.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "ok")
                    {
                        resolvedVideoUrl = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                        videoTitle = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VideoSearch pipeline call failed for query: {Query}", searchTerm);
            }

            // Fall back to LLM's video_url if failed
            if (string.IsNullOrEmpty(resolvedVideoUrl) && !string.IsNullOrEmpty(videoUrl))
                resolvedVideoUrl = videoUrl;

            if (!string.IsNullOrEmpty(resolvedVideoUrl))
            {
                yield return FormatSse("tool", new { name = "VideoSearch", state = "end", summary = !string.IsNullOrEmpty(videoTitle) ? $"Found: {videoTitle}" : "Video found" });
                yield return FormatSse("video", new { url = resolvedVideoUrl, message = previewMessage });

                await conversationContextManager
                    .AddMessageToContextAsync(userId, actualSessionId, $"{previewMessage}\n[Video: {resolvedVideoUrl}]", "assistant");
            }
            else
            {
                yield return FormatSse("tool", new { name = "VideoSearch", state = "end", summary = "No embeddable video found" });
            }
        }
        else if (interactionType == LLMInteractionType.ChatWithAI || interactionType == LLMInteractionType.ChooseMethod
                 || interactionType == LLMInteractionType.TopicPreview)
        {
            string chatPrompt = BuildChatPrompt(conversationContext.ToString(), query);

            var fullResponseBuilder = new StringBuilder();

            try
            {
                await foreach (var chunk in customerHelper.SendStreamRequestToGemini(chatPrompt, useThinking: false, cancellationToken: cancellationToken))
                {
                    if (chunk.StartsWith("__FALLBACK__:"))
                    {
                        var provider = chunk[13..].Trim();
                        yield return FormatSse("fallback", new { provider, message = "Switched to backup AI model" });
                    }
                    else if (!chunk.StartsWith("__STATUS__") && !chunk.StartsWith("__MODEL__") && !chunk.StartsWith("__THINKING_CONTENT__"))
                    {
                        yield return FormatSse("text", new { delta = chunk });
                        fullResponseBuilder.Append(chunk);
                    }
                }
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested && !string.IsNullOrEmpty(fullResponseBuilder.ToString()))
                {
                    await conversationContextManager
                        .AddMessageToContextAsync(userId, actualSessionId, fullResponseBuilder.ToString(), "assistant");
                }
            }
        }
        else
        {
            string finalResponse = "";

            if ((interactionType == LLMInteractionType.RoadmapGeneration || interactionType == LLMInteractionType.CVAnalysis) && toolArguments != null)
            {
                // Career Pulse from CV/roadmap tags — fetch market data if not already done
                if (toolArguments.Tags is { Length: > 0 })
                {
                    string? tagPulseEvent = null;
                    try
                    {
                        var tagArray = toolArguments.Tags.ToArray();
                        fetchedMarketInsight ??= await jobMarketService.GetMarketInsightsAsync(tagArray);

                        if (fetchedMarketInsight is not null)
                        {
                            var tagPulse = jobMarketService.CalculateReadiness(tagArray, fetchedMarketInsight);
                            if (tagPulse is not null)
                            {
                                tagPulseEvent = FormatSse("career_pulse", new
                                {
                                    readinessScore = tagPulse.ReadinessScore,
                                    readinessLevel = tagPulse.ReadinessLevel,
                                    matchedSkills = tagPulse.MatchedSkills,
                                    topGaps = tagPulse.TopGaps.Select(g => new { skill = g.Skill, demandPercent = g.DemandPercent, category = g.Category }),
                                    insight = tagPulse.Insight,
                                    targetRole = tagPulse.TargetRole,
                                    jobsAnalyzed = tagPulse.JobsAnalyzed
                                });
                            }
                        }
                    }
                    catch { /* graceful — don't block roadmap generation */ }

                    if (tagPulseEvent is not null)
                        yield return tagPulseEvent;
                }

                var jobId = Guid.NewGuid().ToString("N")[..12];
                toolArguments.JobId = jobId;

                yield return FormatSse("pipeline_job", new { jobId });

                yield return FormatSse("status", new { message = "Running Bosla Education Pipeline...", step = "tool_execution" });
                if (toolArguments?.Tags != null)
                    yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "start", summary = $"Detected Interests: {string.Join(", ", toolArguments.Tags)}" });

                yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "processing" });

                var apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments!, cancellationToken);

                // Generate a stable ID that links the Cosmos chat message to a saved roadmap
                var generationId = Guid.NewGuid().ToString();

                object? resultData = null;
                try
                {
                    var apiParsed = JsonSerializer.Deserialize<JsonElement>(apiResponse);
                    if (apiParsed.TryGetProperty("data", out var innerData))
                    {
                        resultData = innerData;
                    }
                    else
                    {
                        resultData = apiParsed;
                    }
                }
                catch
                {
                    resultData = apiResponse;
                }

                // Inject generationId into result data so it's stored in Cosmos and sent via SSE
                string resultJsonString;
                try
                {
                    var rawJson = resultData is JsonElement je2 ? je2.GetRawText() : JsonSerializer.Serialize(resultData);
                    resultJsonString = InjectGenerationId(rawJson, generationId);
                    resultData = JsonSerializer.Deserialize<JsonElement>(resultJsonString);
                }
                catch
                {
                    resultJsonString = JsonSerializer.Serialize(new { data = resultData, generationId }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    resultData = JsonSerializer.Deserialize<JsonElement>(resultJsonString);
                }

                finalResponse = $"[SYSTEM]: Roadmap generated successfully.\nDetails: {resultJsonString}";

                yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "end", summary = "Roadmap generated" });

                // Emit LLM text response alongside roadmap result
                if (!string.IsNullOrEmpty(aiResponse))
                {
                    yield return FormatSse("text", new { delta = aiResponse });
                }

                yield return FormatSse("result", new { status = "success", data = resultData });
            }
            else if (interactionType == LLMInteractionType.ChooseTrack)
            {
                finalResponse = "Please select a track from the list below.";
                yield return FormatSse("text", new { delta = finalResponse });
            }
            else
            {
                finalResponse = "I have processed your request.";
                yield return FormatSse("text", new { delta = finalResponse });
            }

            if (!cancellationToken.IsCancellationRequested && !string.IsNullOrEmpty(finalResponse))
            {
                await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, finalResponse, "assistant");
            }
        }

        if (followUpSuggestions is { Length: > 0 })
        {
            yield return FormatSse("suggestions", new { items = followUpSuggestions });
        }

        yield return FormatSse("done", new { });
    }

    public async Task<APIResponse<AiIntentDetectionResponse>> ProcessUserQueryWithIntentDetectionAsync(string userId, string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("User ID is required");
        if (string.IsNullOrWhiteSpace(query))
            throw new BadRequestException("Query is empty");

        try
        {
            var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

            var contextTask = conversationContextManager.GetConversationContextAsync(userId, actualSessionId);
            var addMessageTask = conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");

            await Task.WhenAll(contextTask, addMessageTask);
            var conversationContext = await contextTask;

            var (interactionType, confidence, aiResponse, toolArguments, _, thinkingContent, _, _, videoUrl, videoSearchQuery) = await DetectIntentAsync(query, conversationContext.ToString());

            if ((interactionType == LLMInteractionType.RoadmapGeneration || interactionType == LLMInteractionType.CVAnalysis) && toolArguments != null)
            {
                toolArguments.JobId = Guid.NewGuid().ToString("N")[..12];
                var apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments);
                aiResponse += $"\n\n[SYSTEM]: Roadmap generated successfully.\nDetails: {apiResponse}";
            }

            if (toolArguments?.Tags != null)
            {
                // Using structured logging instead of Console.WriteLine
            }

            var response = new AiIntentDetectionResponse
            {
                InteractionType = interactionType,
                Confidence = confidence,
                Success = true,
                Answer = !string.IsNullOrEmpty(aiResponse) ? aiResponse : null,
                Thinking = !string.IsNullOrEmpty(thinkingContent),
                ThinkingLog = thinkingContent,
                VideoUrl = interactionType == LLMInteractionType.TopicPreview ? videoUrl : null
            };

            if (!string.IsNullOrEmpty(response.Answer))
            {
                await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, response.Answer, "assistant");
            }

            return new APIResponse<AiIntentDetectionResponse>()
            {
                StatusCode = HttpStatusCode.OK,
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new APIResponse<AiIntentDetectionResponse>()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Data = new AiIntentDetectionResponse
                {
                    InteractionType = LLMInteractionType.ChatWithAI,
                    Success = false,
                    ErrorMessage = ex.Message
                }
            };
        }
    }

    private async Task<(LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments, string ModelName, string? ThinkingContent, string? targetRole, string[]? followUpSuggestions, string? videoUrl, string? videoSearchQuery)> DetectIntentAsync(string query, string conversationContext)
    {
        var prompts = options.CurrentValue.Prompts;
        var detectionPrompt = string.Format(prompts.IntentDetectionUserPromptTemplate,
            prompts.IntentDetectionSystemPrompt,
            (!string.IsNullOrEmpty(conversationContext) ? $"Conversation History:\n{conversationContext}\n\n" : ""),
            query);
        try
        {
            var (responseText, modelName, thinkingContent) = await customerHelper.SendRequestToGemini(detectionPrompt, useThinking: true);
            var parsed = ParseCombinedResponse(responseText);
            if (parsed.HasValue)
                return (parsed.Value.intent, parsed.Value.confidence, parsed.Value.response, parsed.Value.toolArguments, modelName, thinkingContent, parsed.Value.targetRole, parsed.Value.followUpSuggestions, parsed.Value.videoUrl, parsed.Value.videoSearchQuery);

            return (LLMInteractionType.ChatWithAI, 0, null, null, modelName, thinkingContent, null, null, null, null);
        }
        catch
        {
            return (LLMInteractionType.ChatWithAI, 0, null, null, "Unknown", null, null, null, null, null);
        }
    }

    private string BuildChatPrompt(string context, string query)
    {
        var prompts = options.CurrentValue.Prompts;
        return string.Format(prompts.ChatUserPromptTemplate,
            prompts.ChatSystemPrompt + "\n" + context,
            query);
    }

    private (LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments, string? targetRole, string[]? followUpSuggestions, string? videoUrl, string? videoSearchQuery)? ParseCombinedResponse(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        try
        {
            var cleaned = responseText.Trim();
            if (cleaned.StartsWith("```json"))
                cleaned = cleaned[7..];
            if (cleaned.StartsWith("```"))
                cleaned = cleaned[3..];
            if (cleaned.EndsWith("```"))
                cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();

            // Try to extract just the JSON object (in case of trailing garbage)
            var firstBrace = cleaned.IndexOf('{');
            var lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned[firstBrace..(lastBrace + 1)];
            }

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var intentStr = root.TryGetProperty("intent", out var intentProp) ? intentProp.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var confProp) && confProp.TryGetInt32(out var conf) ? conf : 0;
            var response = root.TryGetProperty("response", out var respProp) && respProp.ValueKind != JsonValueKind.Null
                ? respProp.GetString()
                : null;

            var toolArguments = root.TryGetProperty("tool_arguments", out var argsProp) && argsProp.ValueKind != JsonValueKind.Null
                ? JsonSerializer.Deserialize<RoadmapRequestDTO>(argsProp.GetRawText())
                : null;

            var targetRole = root.TryGetProperty("target_role", out var roleProp) && roleProp.ValueKind == JsonValueKind.String
                ? roleProp.GetString()
                : null;

            string[]? followUpSuggestions = null;
            if (root.TryGetProperty("follow_up_suggestions", out var sugProp) && sugProp.ValueKind == JsonValueKind.Array)
            {
                followUpSuggestions = [.. sugProp.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Take(3)];
            }

            var videoUrl = root.TryGetProperty("video_url", out var videoProp) && videoProp.ValueKind == JsonValueKind.String
                ? videoProp.GetString()
                : null;

            var videoSearchQuery = root.TryGetProperty("video_search_query", out var vsqProp) && vsqProp.ValueKind == JsonValueKind.String
                ? vsqProp.GetString()
                : null;

            if (!string.IsNullOrEmpty(intentStr) && Enum.TryParse<LLMInteractionType>(intentStr, true, out var intent))
            {
                return (intent, Math.Clamp(confidence, 0, 100), response, toolArguments, targetRole, followUpSuggestions, videoUrl, videoSearchQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response, falling back to general chat. Raw: {ResponseText}", responseText?[..Math.Min(responseText.Length, 200)]);
            return null;
        }

        return null;
    }


    private async Task<string> ExecuteRoadmapGenerationAsync(RoadmapRequestDTO requestData, CancellationToken cancellationToken = default)
    {
        string apiUrl = options.CurrentValue.PipelineApi.BaseUrl;

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(8);

        var jsonContent = JsonSerializer.Serialize(requestData);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(apiUrl, httpContent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadAsStringAsync();
                return resultJson;
            }
            else
            {
                return $"Error: The roadmap service returned {response.StatusCode}";
            }
        }
        catch (TaskCanceledException)
        {
            return "Error: The roadmap service request timed out. Please try again.";
        }
        catch (Exception ex)
        {
            return $"Error calling roadmap service: {ex.Message}";
        }
    }


    private string GenerateSessionId(string userId)
    {
        var input = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLower()[..16];
    }

    public async Task<APIResponse> GetCustomerProfileAsync(string customerId)
    {
        if (string.IsNullOrEmpty(customerId))
            throw new BadRequestException("Customer Id cannot be null or empty");

        var customer = await GetALlCustomerDetailsAsync(customerId) ?? throw new NotFoundException("Customer not found");
        return new APIResponse<CustomerDTO>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = customer,
        };
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await unitOfWork.GetRepo<Customer, string>().GetAllAsync();
    }

    public async Task<Customer> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BadRequestException("Customer ID cannot be empty");
        return await unitOfWork.GetRepo<Customer, string>().GetIdAsync(id);
    }

    public async Task CreateAsync(Customer customer)
    {
        if (customer == null)
            throw new BadRequestException("Customer cannot be null");
        await unitOfWork.GetRepo<Customer, string>().CreateAsync(customer);
    }

    public async Task UpdateAsync(Customer customer)
    {
        if (customer == null)
            throw new BadRequestException("Customer cannot be null");
        await unitOfWork.GetRepo<Customer, string>().UpdateAsync(customer);
    }

    public async Task DeleteAsync(Customer customer)
    {
        if (customer == null)
            throw new BadRequestException("Customer cannot be null");
        await unitOfWork.GetRepo<Customer, string>().DeleteAsync(customer);
    }

    public async Task<CustomerDTO> GetALlCustomerDetailsAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BadRequestException("Customer ID cannot be empty");

        var customer = await unitOfWork.GetRepo<Customer, string>()
            .GetAsync(new CustomerDetailsSpecification(id));

        return mapper.Map<CustomerDTO>(customer);
    }

    public string CreateAiRequest(string userId, AiQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("UserId is required");
        if (request == null)
            throw new BadRequestException("Request is required");
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new BadRequestException("Query is required");

        return aiRequestStore.Create(userId, request);
    }

    public (string UserId, AiQueryRequest Request) GetAiRequest(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new BadRequestException("RequestId is required");

        var stored = aiRequestStore.GetAndRemove(requestId) ?? throw new NotFoundException("Request not found or expired");
        return stored;
    }

    private string FormatSse(string eventName, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return $"event: {eventName}\ndata: {json}";
    }

    private static string[] ExtractKeywordsFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 10)
            return [];

        var words = query.Split([' ', ',', '.', '!', '?', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var keywords = new List<string>();

        foreach (var word in words)
        {
            var clean = word.Trim('\'', '"', '(', ')', '[', ']');
            if (clean.Length < 2) continue;

            var found = SkillDictionary.ExtractSkills(clean);
            if (found.Count > 0)
            {
                keywords.AddRange(found.Keys);
            }
        }

        // Multi-word combinations (e.g. "React Native", "Spring Boot")
        var fullText = string.Join(" ", words);
        var multiWordSkills = SkillDictionary.ExtractSkills(fullText);
        foreach (var (skill, _) in multiWordSkills)
        {
            if (!keywords.Contains(skill, StringComparer.OrdinalIgnoreCase))
                keywords.Add(skill);
        }

        return [.. keywords.Distinct(StringComparer.OrdinalIgnoreCase).Take(5)];
    }

    private static string InjectGenerationId(string json, string generationId)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(json);
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            obj["generationId"] = generationId;
        }
        return node?.ToJsonString() ?? json;
    }
}
