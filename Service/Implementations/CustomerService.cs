using System.Net;
using System.Text.Json;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications;
using Domain.Responses;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;
using Shared.DTOs.CustomerDTOs;
using Shared.Enums;

namespace Service.Implementations;

public class CustomerService(
    IUnitOfWork unitOfWork
    , IMapper mapper
    , CustomerHelper customerHelper
    , ConversationContextManager conversationContextManager) : ICustomerService
{
    public async Task<APIResponse<string>> ProcessUserQueryAsync(string userId, string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new BadRequestException("Query is empty");

        var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");

        var conversationContext = await conversationContextManager.GetConversationContextAsync(userId, actualSessionId);

        string aiPrompt = $"Context:\n{conversationContext}\n\nCurrent User Query: {query}\n\nPlease provide a helpful response considering the conversation history if relevant.";

        var geminiResponse = await customerHelper.SendRequestToGemini(aiPrompt);
        string responseText = customerHelper.ExtractTextFromResponse(geminiResponse);

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, responseText, "assistant");

        return new APIResponse<string>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = responseText
        };
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

            var (interactionType, confidence, aiResponse) = await DetectIntentAndGenerateResponseAsync(query, conversationContext);

            var response = new AiIntentDetectionResponse
            {
                InteractionType = interactionType,
                Confidence = confidence,
                Success = true,
                Answer = !string.IsNullOrEmpty(aiResponse) ? aiResponse : null
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

    private async Task<(LLMInteractionType intent, float confidence, string? response)> DetectIntentAndGenerateResponseAsync(string query, string conversationContext)
    {
        string combinedPrompt = $@"You are Bosla AI assistant. Analyze the user's CURRENT query and conversation context to determine their intent.

Categories:
- CVAnalysis: User wants to analyze/upload their CV NOW, or wants career guidance based on their CV
- RoadmapGeneration: User wants to CREATE a learning roadmap, study plan, or course plan NOW
- ChooseTrack: User wants to SELECT or get recommendations for a learning track NOW
- ChatWithAI: General conversation, questions, follow-ups, or anything else
- ChooseMethod: User is frustrated and doesn't know what to do or what method to use

IMPORTANT: If the user MENTIONS a category topic but is asking a general question, that's ChatWithAI.
Example: ""I have a CV in tech, can you explain what closures are?"" → ChatWithAI (asking about closures, not CV analysis)

{(!string.IsNullOrEmpty(conversationContext) ? $"Conversation History:\n{conversationContext}\n\n" : "")}Current User Query: ""{query}""

Respond in this EXACT JSON format:
{{""intent"": ""<category>"", ""confidence"": <0-100>, ""response"": ""<your helpful response if ChatWithAI, otherwise null>""}}

confidence = how confident you are (0-100%) in this intent classification.

IMPORTANT INSTRUCTIONS for 'response':
- ALWAYS provide a helpful 'response' message for the user, even if the intent is NOT ChatWithAI.
- If CVAnalysis: Respond enthusiastically like ""Great! Please upload your CV and I will analyze it for you.""
- If ChooseMethod: Respond like ""I understand you might be feeling stuck. I'm Bosla AI assistant, and here are the options I can help with:""
- If RoadmapGeneration: Respond like ""I can help you create a personalized learning roadmap. What query or topic do you have in mind?""
- If ChooseTrack: Respond like ""I can help you choose the right track. Tell me about your interests or goals.""
- If ChatWithAI: Provide the natural conversational answer to their query.";

        try
        {
            var responseText = await customerHelper.SendRequestToGemini(combinedPrompt);

            var parsed = ParseCombinedResponse(responseText);
            if (parsed.HasValue)
            {
                return parsed.Value;
            }

            return (LLMInteractionType.ChatWithAI, 0, responseText);
        }
        catch (Exception ex)
        {
            // Bubble up the error so we can see what's happening
            throw new Exception($"LLM Error: {ex.Message}", ex);
        }
    }

    private (LLMInteractionType intent, float confidence, string? response)? ParseCombinedResponse(string? responseText)
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

            if (!string.IsNullOrEmpty(intentStr) && Enum.TryParse<LLMInteractionType>(intentStr, true, out var intent))
            {
                return (intent, Math.Clamp(confidence, 0, 100), response);
            }
        }
        catch
        {
        }

        return null;
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
}