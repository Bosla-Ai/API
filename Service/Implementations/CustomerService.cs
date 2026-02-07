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

        var (geminiResponse, _) = await customerHelper.SendRequestToGemini(aiPrompt);
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

        yield return "__STATUS__:Analyzing your request...";

        var (interactionType, confidence, aiResponse, toolArguments, detectionModel) = await DetectIntentAsync(query, conversationContext.ToString());

        yield return $"__MODEL__:{detectionModel}";

        yield return $"__INTENT__:{interactionType}";

        if (interactionType == LLMInteractionType.ChatWithAI || interactionType == LLMInteractionType.ChooseMethod)
        {
            string chatPrompt = BuildChatPrompt(conversationContext, query);

            var fullResponseBuilder = new StringBuilder();

            try
            {
                await foreach (var chunk in customerHelper.SendStreamRequestToGemini(chatPrompt))
                {
                    yield return chunk;

                    if (!chunk.StartsWith("__STATUS__"))
                    {
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
                yield return "__STATUS__:Running Bosla Education Pipeline...";
                if (toolArguments?.Tags != null) yield return $"__STATUS__:Detected Interests: {string.Join(", ", toolArguments.Tags)}";

                var apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments!);
                finalResponse = $"[SYSTEM]: Roadmap generated successfully.\nDetails: {apiResponse}";

                yield return "__STATUS__:generation complete.";
            }
            else if (interactionType == LLMInteractionType.ChooseTrack)
            {
                finalResponse = "Please select a track from the list below.";
            }
            else
            {
                finalResponse = "I have processed your request.";
            }

            yield return finalResponse;

            await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, finalResponse, "assistant");
        }
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

            var (interactionType, confidence, aiResponse, toolArguments, _) = await DetectIntentAsync(query, conversationContext.ToString());

            if ((interactionType == LLMInteractionType.RoadmapGeneration || interactionType == LLMInteractionType.CVAnalysis) && toolArguments != null)
            {
                var apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments);
                aiResponse += $"\n\n[SYSTEM]: Roadmap generated successfully.\nDetails: {apiResponse}";
            }

            if (toolArguments?.Tags != null)
            {
                Console.WriteLine($"\n[Generated Tags]: {string.Join(", ", toolArguments.Tags)}\n");
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

    private async Task<(LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments, string ModelName)> DetectIntentAsync(string query, string conversationContext)
    {
        string detectionPrompt = $@"You are Bosla AI, an intelligent educational assistant.
Your task is to analyze the user's input, determine their intent for the frontend UI, and if necessary, generate a professional roadmap.

AVAILABLE INTENTS (Frontend will render UI based on this):
- CVAnalysis: User WANTS to upload/analyze their CV (triggers CV upload component).
- ChooseTrack: User wants to SELECT a learning track (triggers track selection component).
- RoadmapGeneration: User wants a study roadmap (triggers roadmap generation pipeline).
- ChooseMethod: User is confused about what to do.
- ChatWithAI: General conversation/questions.

*** ANALYSIS RULES & LOGIC ***

1. **CVAnalysis Intent (Two Scenarios):**
   a) **User WANTS to upload CV** (e.g., 'analyze my resume', 'review my cv'):
      - Intent = 'CVAnalysis'
      - tool_arguments = null (frontend shows upload UI).
      - Response: Invite them to upload (AR: ""تمام! ياريت ترفع الـ CV..."" / EN: ""Great! Please upload..."").

   b) **User SENDS CV JSON data** (contains 'profile', 'verifiedSkills', etc.):
      - Intent = 'CVAnalysis'
      - **ACTION: PERFORM GAP ANALYSIS (See 'Gap Analysis Protocol' below).**
      - Response: Summarize gaps (AR: ""بناءً على دورك، ناقصك مهارات زي..."" / EN: ""Based on your role, you are missing..."").

2. **ChooseTrack Intent:**
   - User wants to pick a path/track.
   - Intent = 'ChooseTrack'
   - tool_arguments = null.

3. **RoadmapGeneration Intent:**
   - User asks for a roadmap OR Frontend sends selected tags.
   - **ACTION: PERFORM GAP ANALYSIS (See 'Gap Analysis Protocol' below)** to ensure the roadmap is professional.
   - tool_arguments = {{ tags, prefer_paid, language, sources }}

4. **ChooseMethod Intent:**
   - User is confused.
   - Intent = 'ChooseMethod'

5. **ChatWithAI Intent:**
   - General conversation.
   - Intent = 'ChatWithAI'

*** GAP ANALYSIS PROTOCOL (THE BRAIN) ***
When generating 'tags' for Roadmap/CV, identify missing ""Silent Pillars"" required for a SENIOR professional in their TARGET ROLE.

   **Step 1: Identify Role** (e.g., Full Stack, CyberSec, DevOps, Embedded).
   **Step 2: Check for the 3 Professional Pillars (If missing, ADD them as tags):**
      A. **The Operational Gap** (Delivery/Ops):
         - Web: CI/CD, Docker, Cloud (AWS).
         - Cyber: Reporting, SIEM, Scripting.
         - Embedded: RTOS, Hardware Debugging.
      B. **The Quality Gap** (Verification):
         - Web: Testing (Jest/xUnit), SonarQube.
         - Cyber: Compliance (NIST), Risk Assessment.
         - Data: Data Cleaning, Validation.
      C. **The Advanced Gap** (Scale/Depth):
         - Web: System Design, Microservices, Security.
         - Cyber: Reverse Engineering, Threat Hunting.
         - General: Design Patterns, Clean Architecture.

   **Step 3: Tag Generation Rule (STRICT ATOMICITY):**
   - **Output 5-8 distinct tags.**
   - **CRITICAL: NEVER combine topics with '&', 'and', or '/'.**
     - BAD: ""Docker & Kubernetes""
     - GOOD: ""Docker"", ""Kubernetes""
     - BAD: ""PostgreSQL and TypeORM""
     - GOOD: ""PostgreSQL"", ""TypeORM""
   - Mix: [2 Advanced Ecosystem Tools] + [3 Gap Filling Tags from above].
   - Tags must be search-friendly (e.g., ""Python Testing"" not just ""Quality"").

*** PARAMETER EXTRACTION RULES ***
- **prefer_paid**: true if user mentions ""paid"", ""course"", ""udemy"". Default false.
- **sources**:
  - If paid: default ['udemy'].
  - If free: default ['youtube'].
- **language**: 'ar' or 'en' based on user input language.

{(!string.IsNullOrEmpty(conversationContext) ? $"Conversation History:\n{conversationContext}\n\n" : "")}
Current User Query: ""{query}""

Respond in EXACT JSON:
{{
  ""intent"": ""RoadmapGeneration"" | ""ChatWithAI"" | ""CVAnalysis"" | ""ChooseTrack"" | ""ChooseMethod"",
  ""confidence"": <0-100>,
  ""response"": ""<Message to show the user IN THEIR LANGUAGE>"",
  ""tool_arguments"": {{
      ""tags"": [""tag1"", ""tag2"", ...],
      ""prefer_paid"": <bool>,
      ""language"": ""en"" | ""ar"",
      ""sources"": [""youtube"", ""udemy"", ...] or null
  }} or null
}}";
        try
        {
            var (responseText, modelName) = await customerHelper.SendRequestToGemini(detectionPrompt);
            var parsed = ParseCombinedResponse(responseText);
            if (parsed.HasValue)
                return (parsed.Value.intent, parsed.Value.confidence, parsed.Value.response, parsed.Value.toolArguments, modelName);

            // Fallback
            return (LLMInteractionType.ChatWithAI, 0, null, null, modelName);
        }
        catch
        {
            return (LLMInteractionType.ChatWithAI, 0, null, null, "Unknown");
        }
    }

    private string BuildChatPrompt(string context, string query)
    {
        return $@"You are Bosla AI, an intelligent educational assistant.
System: Be helpful, encouraging, and concise.

Conversation History:
{context}

Current User Query: {query}

Thought Process Visibility:
If you need to perform analysis, search memory, or plan your answer, output your thought on a new line starting with >>>.
Example:
>>> Analyzing user's .NET background...
>>> Formulating explanation...
Then provide your actual response.";
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