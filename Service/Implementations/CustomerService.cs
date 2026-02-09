using System.Net;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications;
using Domain.Responses;
using Microsoft.Extensions.Configuration;
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
    , CustomerHelper customerHelper
    , ConversationContextManager conversationContextManager
    , IOptionsMonitor<AiOptions> options
    , IHttpClientFactory httpClientFactory
    , AiRequestStore aiRequestStore) : ICustomerService
{
    public async Task<APIResponse<string>> ProcessUserQueryAsync(string userId, string query, string? sessionId = null)
    {
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

    public async IAsyncEnumerable<string> ProcessUserQueryStreamAsync(string userId, string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

        var contextTask = conversationContextManager.GetConversationContextAsync(userId, actualSessionId);
        var addMessageTask = conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");
        await Task.WhenAll(contextTask, addMessageTask);
        var conversationContext = await contextTask;

        yield return FormatSse("status", new { message = "Analyzing your request...", step = "init" });

        // --- Start Streaming Intent Detection ---
        var prompts = options.CurrentValue.Prompts;
        var detectionPrompt = string.Format(prompts.IntentDetectionUserPromptTemplate,
            prompts.IntentDetectionSystemPrompt,
            (!string.IsNullOrEmpty(conversationContext.ToString()) ? $"Conversation History:\n{conversationContext}\n\n" : ""),
            query);

        var detectionJsonBuilder = new StringBuilder();
        string detectionModel = "Unknown";
        string thinkingLog = "";
        string? thinkingTitle = null;
        bool titleExtracted = false;

        // Stream the detection process to catch thoughts in real-time
        await foreach (var chunk in customerHelper.SendStreamRequestToGemini(detectionPrompt, useThinking: true))
        {
            if (chunk.StartsWith("__MODEL__:"))
            {
                detectionModel = chunk.Substring(10).Trim();
                yield return FormatSse("model", new { name = detectionModel });
            }
            else if (chunk.StartsWith("__THINKING_CONTENT__:"))
            {
                var content = chunk.Substring(21);
                thinkingLog += content;

                // Title Extraction Logic: First line is title, rest is debug log
                if (!titleExtracted)
                {
                    if (thinkingLog.Contains("\n"))
                    {
                        var firstNewLineIndex = thinkingLog.IndexOf('\n');
                        thinkingTitle = thinkingLog.Substring(0, firstNewLineIndex).Trim('*', ' ', '#'); // Clean md formatting
                        titleExtracted = true;

                        // Emit title
                        yield return FormatSse("thinking", new { title = thinkingTitle, debug = "" });

                        // Emit remainder as debug
                        var remainder = thinkingLog.Substring(firstNewLineIndex + 1);
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

        // --- End Streaming Intent Detection ---

        yield return FormatSse("intent", new { name = interactionType.ToString(), confidence = confidence });

        if (interactionType == LLMInteractionType.ChatWithAI || interactionType == LLMInteractionType.ChooseMethod)
        {
            string chatPrompt = BuildChatPrompt(conversationContext.ToString(), query);

            var fullResponseBuilder = new StringBuilder();

            try
            {
                await foreach (var chunk in customerHelper.SendStreamRequestToGemini(chatPrompt, useThinking: false))
                {
                    if (!chunk.StartsWith("__STATUS__") && !chunk.StartsWith("__MODEL__") && !chunk.StartsWith("__THINKING_CONTENT__"))
                    {
                        yield return FormatSse("text", new { delta = chunk });
                        fullResponseBuilder.Append(chunk);
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(fullResponseBuilder.ToString()))
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
                var jobId = Guid.NewGuid().ToString("N")[..12];
                toolArguments.JobId = jobId;

                yield return FormatSse("pipeline_job", new { jobId });

                yield return FormatSse("status", new { message = "Running Bosla Education Pipeline...", step = "tool_execution" });
                if (toolArguments?.Tags != null)
                    yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "start", summary = $"Detected Interests: {string.Join(", ", toolArguments.Tags)}" });

                yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "processing" });

                var apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments!);
                finalResponse = $"[SYSTEM]: Roadmap generated successfully.\nDetails: {apiResponse}";

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

                yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "end", summary = "Roadmap generated" });
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

            if (!string.IsNullOrEmpty(finalResponse))
            {
                await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, finalResponse, "assistant");
            }
        }

        yield return FormatSse("done", new { });
    }

    public async Task<APIResponse<AiIntentDetectionResponse>> ProcessUserQueryWithIntentDetectionAsync(string userId, string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new BadRequestException("Query is empty");

        try
        {
            var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

            var contextTask = conversationContextManager.GetConversationContextAsync(userId, actualSessionId);
            var addMessageTask = conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");

            await Task.WhenAll(contextTask, addMessageTask);
            var conversationContext = await contextTask;

            var (interactionType, confidence, aiResponse, toolArguments, _, thinkingContent) = await DetectIntentAsync(query, conversationContext.ToString());

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
                ThinkingLog = thinkingContent
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

    private async Task<(LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments, string ModelName, string? ThinkingContent)> DetectIntentAsync(string query, string conversationContext)
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
                return (parsed.Value.intent, parsed.Value.confidence, parsed.Value.response, parsed.Value.toolArguments, modelName, thinkingContent);

            // Fallback
            return (LLMInteractionType.ChatWithAI, 0, null, null, modelName, thinkingContent);
        }
        catch
        {
            return (LLMInteractionType.ChatWithAI, 0, null, null, "Unknown", null);
        }
    }

    private string BuildChatPrompt(string context, string query)
    {
        var prompts = options.CurrentValue.Prompts;
        return string.Format(prompts.ChatUserPromptTemplate,
            prompts.ChatSystemPrompt + "\n" + context,
            query);
    }

    private (LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments)? ParseCombinedResponse(string? responseText)
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

            if (!string.IsNullOrEmpty(intentStr) && Enum.TryParse<LLMInteractionType>(intentStr, true, out var intent))
            {
                return (intent, Math.Clamp(confidence, 0, 100), response, toolArguments);
            }
        }
        catch (Exception ex)
        {
            throw new BadRequestException($"Failed to parse AI response: {ex.Message}");
        }

        return null;
    }


    private async Task<string> ExecuteRoadmapGenerationAsync(RoadmapRequestDTO requestData)
    {
        string apiUrl = options.CurrentValue.PipelineApi.BaseUrl;

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        var jsonContent = JsonSerializer.Serialize(requestData);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(apiUrl, httpContent);

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
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLower()[..16];
    }

    public async Task<APIResponse> GetCustomerProfileAsync(string customerId)
    {
        if (string.IsNullOrEmpty(customerId))
            throw new BadRequestException("Customer Id cannot be null or empty");

        var customer = await GetALlCustomerDetailsAsync(customerId);

        if (customer == null)
            throw new NotFoundException("Customer not found");

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
        return await unitOfWork.GetRepo<Customer, string>().GetIdAsync(id);
    }

    public async Task CreateAsync(Customer customer)
    {
        await unitOfWork.GetRepo<Customer, string>().CreateAsync(customer);
    }

    public async Task UpdateAsync(Customer customer)
    {
        await unitOfWork.GetRepo<Customer, string>().UpdateAsync(customer);
    }

    public async Task DeleteAsync(Customer customer)
    {
        await unitOfWork.GetRepo<Customer, string>().DeleteAsync(customer);
    }

    public async Task<CustomerDTO> GetALlCustomerDetailsAsync(string id)
    {
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

        var stored = aiRequestStore.GetAndRemove(requestId);
        if (stored == null)
            throw new NotFoundException("Request not found or expired");

        return stored.Value;
    }

    private string FormatSse(string eventName, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return $"event: {eventName}\ndata: {json}";
    }
}