using System.Net;
using System.Text.Json;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications;
using Domain.Responses;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;
using Shared.DTOs.CustomerDTOs;
using Shared.DTOs.RoadmapDTOs;
using Shared.Enums;

namespace Service.Implementations;

public class CustomerService(
    IUnitOfWork unitOfWork
    , IMapper mapper
    , CustomerHelper customerHelper
    , ConversationContextManager conversationContextManager
    , IConfiguration configuration
    , IHttpClientFactory httpClientFactory) : ICustomerService
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

            var (interactionType, confidence, aiResponse, toolArguments) = await DetectIntentAndGenerateResponseAsync(query, conversationContext);

            // Execute Tool if needed (RoadmapGeneration or CVAnalysis with skill gaps)
            if ((interactionType == LLMInteractionType.RoadmapGeneration || interactionType == LLMInteractionType.CVAnalysis) && toolArguments != null)
            {
                var apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments);
                aiResponse += $"\n\n[SYSTEM]: Roadmap generated successfully.\nDetails: {apiResponse}";
            }

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

    private async Task<(LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments)> DetectIntentAndGenerateResponseAsync(string query, string conversationContext)
    {
        string combinedPrompt = $@"You are Bosla AI, an intelligent educational assistant.
Your task is to analyze the user's input and determine their intent for frontend UI rendering.

AVAILABLE INTENTS (Frontend will render UI based on this):
- CVAnalysis: User WANTS to upload/analyze their CV (triggers CV upload component)
- ChooseTrack: User wants to SELECT a learning track (triggers track selection component)
- RoadmapGeneration: User wants a study roadmap with specific topics
- ChooseMethod: User is confused about what to do
- ChatWithAI: General conversation/questions

IMPORTANT: The intent you return controls which UI component the frontend displays!

ANALYSIS RULES:

1. **CVAnalysis Intent (Two Scenarios):**
   a) User WANTS to upload CV (e.g., 'let me upload my CV', 'analyze my resume', 'I want CV analysis'):
      - Intent = 'CVAnalysis'
      - tool_arguments = null (frontend will show upload component)
   
   b) User SENDS CV JSON data (contains 'profile', 'verifiedSkills', 'capabilities'):
      - Intent = 'CVAnalysis'
      - Extract 'profile.primaryRole' as TARGET ROLE
      - Compare skills against LABOR MARKET requirements for that role
      - Identify SKILL GAPS and put them as 'tags' in tool_arguments
      - Example: primaryRole 'Backend Developer' with [.NET, SQL] but market needs Docker, K8s → tags = [""docker"", ""kubernetes""]

2. **ChooseTrack Intent:**
   - User WANTS to choose/select a learning track, pick a path, browse tracks
   - Intent = 'ChooseTrack'
   - tool_arguments = null (frontend will show track selection component)

3. **RoadmapGeneration Intent (Two Scenarios):**
   a) User asks for roadmap WITH specific topics (e.g., 'Give me a React roadmap')
   b) Frontend sends back TagsPayload from track selection (tags array in query)
   
   In BOTH cases:
   - Intent = 'RoadmapGeneration'
   - Extract topics as 'tags'
   - tool_arguments = {{ tags, prefer_paid, language, sources }}

4. **ChooseMethod Intent:**
   - User is confused, doesn't know what to do, asks 'what can you help with?'
   - Intent = 'ChooseMethod'

5. **ChatWithAI Intent:**
   - General questions, follow-ups, conversation
   - Intent = 'ChatWithAI'

{(!string.IsNullOrEmpty(conversationContext) ? $"Conversation History:\n{conversationContext}\n\n" : "")}
Current User Query: ""{query}""

Respond in this EXACT JSON format:
{{
  ""intent"": ""RoadmapGeneration"" | ""ChatWithAI"" | ""CVAnalysis"" | ""ChooseTrack"" | ""ChooseMethod"",
  ""confidence"": <number 0-100>,
  ""response"": ""<Message to show the user IN THEIR LANGUAGE>"",
  ""tool_arguments"": {{
      ""tags"": [""topic_1"", ""topic_2"", ...],
      ""prefer_paid"": <bool>,
      ""language"": ""en"" | ""ar"",
      ""sources"": [""youtube"" | ""udemy"" | ""coursera""] or null
  }} or null
}}

IMPORTANT GUIDELINES for 'tool_arguments' (RoadmapGeneration):
- **prefer_paid**:
  - Set to 'true' if user wants paid courses, premium content, or explicitly mentions Udemy/Coursera.
  - Set to 'false' if user wants free content or explicitly mentions YouTube.
  
- **sources** (Array of specific platforms):
  - IF prefers paid (prefer_paid=true): Default is 'udemy'. You can include 'coursera' if mentioned.
  - IF prefers free (prefer_paid=false): MUST be ['youtube'] (or null, which pipeline treats as youtube).
  - Explicit platform mentions override defaults (e.g., ""free roadmap from udemy"" -> prefer_paid=true (since udemy is paid) + sources=['udemy']).

IMPORTANT GUIDELINES for 'response':
- If CVAnalysis (user wants to upload): Invite them to upload.
  - AR: ""تمام! ياريت ترفع الـ CV بتاعك عشان أقدر أحللهولك.""
  - EN: ""Great! Please upload your CV and I will analyze it for you.""

- If CVAnalysis (with CV data): Analyze and summarize.
  - AR: ""حللت الـ CV بتاعك! بناءً على دور [role] ومتطلبات سوق العمل، محتاج تتعلم: [gaps]. هجهزلك خارطة طريق.""
  - EN: ""I've analyzed your CV! Based on [role] and market requirements, you should learn: [gaps]. I'll generate a roadmap.""

- If ChooseTrack: Invite them to select.
  - AR: ""تمام! اختار التراك اللي يناسبك من القائمة.""
  - EN: ""Great! Please select the track that suits you from the list.""
  
- If ChooseMethod: Offer help options.
  - AR: ""أنا فاهم إنك محتار. أنا مساعد بوصلة، ودول الحاجات اللي ممكن أساعدك فيها:""
  - EN: ""I understand you might be feeling stuck. Here are the options I can help with:""

- If RoadmapGeneration: Confirm.
  - AR: ""تمام، هبدأ أجهزلك خارطة طريق مخصصة عشانك.""
  - EN: ""I'll generate a personalized roadmap for you.""

- If ChatWithAI: Provide natural conversational answer.
";
        try
        {
            var responseText = await customerHelper.SendRequestToGemini(combinedPrompt);

            var parsed = ParseCombinedResponse(responseText);
            if (parsed.HasValue)
            {
                return parsed.Value;
            }

            return (LLMInteractionType.ChatWithAI, 0, responseText, null);
        }
        catch (Exception ex)
        {
            // Bubble up the error so we can see what's happening
            throw new Exception($"LLM Error: {ex.Message}", ex);
        }
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
        catch
        {
        }

        return null;
    }


    private async Task<string> ExecuteRoadmapGenerationAsync(RoadmapRequestDTO requestData)
    {
        string apiUrl = configuration["PipelineApi:BaseUrl"]!;

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
}