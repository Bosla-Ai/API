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
using Microsoft.AspNetCore.Http;
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
    , IJobMarketService jobMarketService
    , IStackExchangeService stackExchangeService
    , ITechEcosystemService techEcosystemService
    , IHttpContextAccessor httpContextAccessor
    , IChatRepository chatRepository
    , IUserProfileRepository userProfileRepository) : ICustomerService
{
    private bool IsSuperAdmin()
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.IsInRole(StaticData.SuperAdminRoleName) ?? false;
    }
    public async Task<APIResponse<string>> ProcessUserQueryAsync(string userId, string query, string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("User ID is required");
        if (string.IsNullOrWhiteSpace(query))
            throw new BadRequestException("Query is empty");

        var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");

        var conversationContext = await conversationContextManager.GetConversationContextAsync(userId, actualSessionId);

        var prompts = options.CurrentValue.Prompts;
        var chatSystemPrompt = prompts.ChatSystemPrompt;

        string aiPrompt = $"Context:\n{conversationContext}\n\nCurrent User Query: {query}\n\nPlease provide a helpful response considering the conversation history if relevant.";

        var (geminiResponse, _, _) = await customerHelper.SendRequestToGemini(aiPrompt, useThinking: false, userId: userId, isSuperAdmin: IsSuperAdmin(), systemPrompt: chatSystemPrompt);
        string responseText = geminiResponse;

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, responseText, "assistant");

        return new APIResponse<string>()
        {
            StatusCode = HttpStatusCode.OK,
            Data = responseText
        };
    }

    public async IAsyncEnumerable<string> ProcessUserQueryStreamAsync(string userId, string query, string? sessionId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        ChatMode chatMode = ChatMode.Fast)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new BadRequestException("User ID is required");
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        var actualSessionId = !string.IsNullOrEmpty(sessionId) ? sessionId : GenerateSessionId(userId);

        // Strip [Active Mode: X] prefix sent by the frontend; extract mode for intent routing.
        string? uiSelectedMode = null;
        var modeMatch = System.Text.RegularExpressions.Regex.Match(query, @"^\[Active Mode:\s*(.+?)\]\s*\n?");
        if (modeMatch.Success)
        {
            uiSelectedMode = modeMatch.Groups[1].Value.Trim();
            query = query[modeMatch.Length..].TrimStart();
            _logger.LogDebug("UI mode override detected: {Mode}", uiSelectedMode);
        }

        // Detect user language for hard constraint injection
        var detectedLanguage = DetectLanguage(query);

        var pendingSseEvents = new ConcurrentQueue<string>();
        conversationContextManager.OnSseEvent = (eventName, data) =>
        {
            pendingSseEvents.Enqueue(FormatSse(eventName, data));
        };

        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");
        await TryPersistProfileFromLatestMessageAsync(userId, query);

        var conversationContext = await conversationContextManager.GetConversationContextAsync(userId, actualSessionId);
        var conversationContextText = conversationContext.ToString();

        // Build set of previously asked question IDs to avoid repeating them
        var previouslyAskedIds = ExtractAskedQuestionIds(conversationContextText);

        // Drain any buffered SSE events (e.g. compaction)
        while (pendingSseEvents.TryDequeue(out var sseEvent))
            yield return sseEvent;

        yield return FormatSse("status", new { message = "Analyzing your request...", step = "init" });

        // Emit quota info for Powerful mode
        if (chatMode == ChatMode.Deep)
        {
            var remaining = customerHelper.GetGeminiRemainingQuota(userId, IsSuperAdmin());
            yield return FormatSse("quota", new
            {
                mode = "deep",
                remaining,
                limit = options.CurrentValue.Gemini.MaxRequestsPerUserPerDay
            });
        }

        string marketContext = "";
        MarketInsightDTO? fetchedMarketInsight = null;
        var marketKeywords = RoadmapIntentHelper.ExtractKeywordsFromText(query);
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

        // Enrich market context with StackExchange tag popularity data
        if (marketKeywords.Length > 0)
        {
            try
            {
                // Fetch StackExchange and npm/GitHub data in parallel
                var tagInsightsTask = stackExchangeService.GetTagInsightsAsync(marketKeywords, cancellationToken);
                var ecosystemTask = techEcosystemService.GetEcosystemInsightsAsync(marketKeywords, cancellationToken);
                await Task.WhenAll(tagInsightsTask, ecosystemTask);
                cancellationToken.ThrowIfCancellationRequested();

                var tagInsights = await tagInsightsTask;
                if (tagInsights is not null)
                {
                    var tagContext = tagInsights.ToPromptContext();
                    if (!string.IsNullOrEmpty(tagContext))
                    {
                        marketContext = string.IsNullOrEmpty(marketContext)
                            ? tagContext
                            : $"{marketContext}\n\n{tagContext}";
                    }
                }

                var ecosystem = await ecosystemTask;
                if (ecosystem is not null)
                {
                    var ecoContext = ecosystem.ToPromptContext();
                    if (!string.IsNullOrEmpty(ecoContext))
                    {
                        marketContext = string.IsNullOrEmpty(marketContext)
                            ? ecoContext
                            : $"{marketContext}\n\n{ecoContext}";
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Supplementary market enrichment failed");
            }
        }

        // Skip intent detection for very short inputs to avoid LLM provider errors.
        var wordCount = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var minWordThreshold = options.CurrentValue.Llm.MinimalInputWordThreshold;
        var skipIntentDetection = wordCount < minWordThreshold;
        var roadmapFlowState = await GetRoadmapFlowStateAsync(userId, actualSessionId);
        if (roadmapFlowState == RoadmapIntentHelper.RoadmapStateCompleted)
        {
            await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
            roadmapFlowState = RoadmapIntentHelper.RoadmapStateIdle;
        }
        var hasPendingRoadmapConfirmation = roadmapFlowState == RoadmapIntentHelper.RoadmapStatePendingConfirmation;
        var hasAskedDiscovery = roadmapFlowState == RoadmapIntentHelper.RoadmapStateDiscoveryAsked;

        var explicitRoadmapRequest = IsExplicitRoadmapRequest(query);
        var hasRoadmapContext = hasPendingRoadmapConfirmation || hasAskedDiscovery || RoadmapIntentHelper.HasRoadmapContext(conversationContextText);
        var roadmapDeclineReply = HasRoadmapDecline(query) && hasRoadmapContext;
        var roadmapConfirmationReply = HasRoadmapConfirmation(query) && hasRoadmapContext;
        var roadmapStateFollowUp = (hasPendingRoadmapConfirmation || hasAskedDiscovery)
            && !roadmapDeclineReply
            && !roadmapConfirmationReply
            && !explicitRoadmapRequest;
        var roadmapRefinementReply = hasRoadmapContext
            && !explicitRoadmapRequest
            && !roadmapDeclineReply
            && !roadmapConfirmationReply
            && await IsRoadmapRefinementSemanticAsync(query, conversationContextText, cancellationToken);

        if (roadmapDeclineReply)
        {
            await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
        }

        // Short confirmations ("yes", "نعم") must still execute roadmap flow.
        if (skipIntentDetection && (roadmapConfirmationReply || roadmapRefinementReply))
        {
            skipIntentDetection = false;
        }

        var enableModeClassification = options.CurrentValue.Llm.EnableModeClassification;
        var sessionMessageThreshold = options.CurrentValue.Llm.NewSessionMessageThreshold;
        string classifiedMode = "ACTION"; // Default to current behavior
        int sessionMessageCount = 0;
        string? modeClassificationError = null;
        UserProfileEntity? userProfile = null;

        try
        {
            userProfile = await userProfileRepository.GetByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user profile for {UserId}, continuing without profile", userId);
        }

        var profileSummary = userProfile?.ToPromptSummary() ?? "No profile data yet";
        var hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);

        if (enableModeClassification && !skipIntentDetection && chatMode != ChatMode.Deep && !explicitRoadmapRequest)
        {
            yield return FormatSse("status", new { message = "Understanding your request...", step = "mode_classification" });

            // Get session message count for context
            sessionMessageCount = await conversationContextManager.GetMessageCountAsync(userId, actualSessionId);

            var profileComplete = userProfile != null
                && (!string.IsNullOrEmpty(userProfile.TargetRole) || userProfile.Interests?.Count > 0);

            // New sessions with incomplete profiles lean toward FRIEND mode.
            var applyFriendBias = sessionMessageCount < sessionMessageThreshold && !profileComplete;

            (classifiedMode, modeClassificationError) = await ClassifyModeWithFallbackAsync(
                customerHelper, query, sessionMessageCount, profileComplete, applyFriendBias, _logger);
        }

        if (sessionMessageCount > 0)
        {
            yield return FormatSse("mode", new { classified = classifiedMode, sessionMessages = sessionMessageCount });
        }

        if (userProfile != null)
        {
            yield return FormatSse("profile", new
            {
                hasProfile = true,
                targetRole = userProfile.TargetRole,
                interestsCount = userProfile.Interests?.Count ?? 0,
                extractionCount = userProfile.ExtractionCount
            });
        }


        // ── Change F (continued): UI mode override routing ──
        // Map UI mode selection to forced intent; intent detection still runs for tags/response.
        LLMInteractionType? uiForcedIntent = null;
        string? canonicalUiMode = null;
        if (!string.IsNullOrEmpty(uiSelectedMode))
        {
            var modeLower = uiSelectedMode.ToLowerInvariant();
            if (modeLower.Contains("roadmap"))
            {
                uiForcedIntent = LLMInteractionType.RoadmapGeneration;
                canonicalUiMode = "Roadmap Builder";
                classifiedMode = "ACTION";
                if (wordCount >= minWordThreshold) skipIntentDetection = false; // preserve short-input guard
                explicitRoadmapRequest = true;
                _logger.LogDebug("UI mode override: Roadmap Builder → forcing RoadmapGeneration intent");
            }
            else if (modeLower.Contains("cv") || modeLower.Contains("resume"))
            {
                uiForcedIntent = LLMInteractionType.CVAnalysis;
                canonicalUiMode = "CV Analyzer";
                classifiedMode = "ACTION";
                if (wordCount >= minWordThreshold) skipIntentDetection = false;
                _logger.LogDebug("UI mode override: CV Analyzer → forcing CVAnalysis intent");
            }
            else if (modeLower.Contains("career") || modeLower.Contains("coach"))
            {
                uiForcedIntent = LLMInteractionType.ChatWithAI;
                canonicalUiMode = "Career Coach";
                classifiedMode = "FRIEND";
                _logger.LogDebug("UI mode override: Career Coach → forcing FRIEND mode");
            }

            yield return FormatSse("mode", new { classified = classifiedMode, uiOverride = canonicalUiMode ?? uiSelectedMode });

            if (uiForcedIntent.HasValue && uiForcedIntent.Value != LLMInteractionType.RoadmapGeneration)
            {
                if (hasAskedDiscovery || hasPendingRoadmapConfirmation)
                {
                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
                }
                hasAskedDiscovery = false;
                hasPendingRoadmapConfirmation = false;
                roadmapStateFollowUp = false;
            }
        }

        // FRIEND mode: Skip intent detection, go directly to warm chat
        if (classifiedMode == "FRIEND")
        {
            skipIntentDetection = true;
            _logger.LogDebug("FRIEND mode: Skipping intent detection for empathetic conversation");
        }

        // UNCLEAR mode: Skip intent detection, ask clarifying question
        if (classifiedMode == "UNCLEAR")
        {
            skipIntentDetection = true;
            yield return FormatSse("text", new { delta = "I'd love to help! Could you tell me a bit more about what you're looking for? For example:\n- Are you exploring career options?\n- Do you have a specific skill you want to learn?\n- Or would you like to chat about your career journey?" });
            yield return FormatSse("done", new { message = "Clarification requested" });

            await conversationContextManager.AddMessageToContextAsync(
                userId, actualSessionId,
                "I'd love to help! Could you tell me a bit more about what you're looking for?",
                "assistant");
            yield break;
        }

        // Intent Detection (only for ACTION mode)
        var prompts = options.CurrentValue.Prompts;

        var systemPrompt = prompts.IntentDetectionSystemPrompt;
        if (chatMode == ChatMode.Deep)
            systemPrompt = "Use your thinking capability to reason step-by-step before classifying the user's intent.\n\n" + systemPrompt;

        // Inject shared language rules
        if (!string.IsNullOrEmpty(prompts.LanguageRules))
            systemPrompt = systemPrompt.Replace("{LANGUAGE_RULES}", prompts.LanguageRules);

        if (!string.IsNullOrEmpty(marketContext))
        {
            systemPrompt = systemPrompt.Replace("{MARKET_CONTEXT}", marketContext);
        }
        else
        {
            systemPrompt = systemPrompt.Replace("{MARKET_CONTEXT}",
                "No real-time market data available. Use your knowledge of current labor market trends.");
        }

        // Hard language constraint based on detected user language
        var langName = detectedLanguage == "ar" ? "Arabic" : "English";
        systemPrompt = $"[LANGUAGE CONSTRAINT: You MUST respond in {langName} only. This overrides all other language rules.]\n\n" + systemPrompt;

        // Inject UI mode context using canonical server-side label (prevents prompt injection).
        var missingProfileFields = GetMissingProfileFields(userProfile);
        var discoveryAttemptCount = await GetDiscoveryAttemptCountAsync(userId, actualSessionId);

        if (!string.IsNullOrEmpty(canonicalUiMode))
        {
            systemPrompt += $"\n\nUI MODE CONTEXT: The user has explicitly selected \"{canonicalUiMode}\" mode. " +
                $"Generate your response, questions, and tags appropriate for this mode.";
        }

        var isNonRoadmapUiOverride = uiForcedIntent.HasValue
            && uiForcedIntent.Value != LLMInteractionType.RoadmapGeneration;

        var shouldApplyRoadmapGapInstruction = missingProfileFields.Count > 0
            && !isNonRoadmapUiOverride
            && (hasAskedDiscovery || hasPendingRoadmapConfirmation || explicitRoadmapRequest
                || (uiForcedIntent == LLMInteractionType.RoadmapGeneration));

        if (shouldApplyRoadmapGapInstruction && discoveryAttemptCount < 2)
        {
            var missingList = string.Join(", ", missingProfileFields);
            systemPrompt += $"\n\nDYNAMIC PROFILE GAP INSTRUCTION:\n" +
                $"The user wants a roadmap but their profile is INCOMPLETE. Missing fields: [{missingList}].\n" +
                $"You MUST ask the user natural, friendly questions to gather this specific information.\n" +
                $"Use the `questions` field in your JSON response with appropriate question types " +
                $"(\"checkbox\" for experience level, \"text\" for target role).\n" +
                $"Set tool_arguments=null until these fields are provided.\n" +
                $"Discovery attempt: {discoveryAttemptCount + 1} of 2 max.";
        }
        else if (shouldApplyRoadmapGapInstruction)
        {
            systemPrompt += "\n\nDYNAMIC PROFILE GAP INSTRUCTION:\n" +
                "Max discovery attempts have been reached. Do NOT ask additional discovery questions. " +
                "Proceed with partial profile and ask for roadmap confirmation.";
        }

        var interactionType = LLMInteractionType.ChatWithAI;
        float confidence = 0;
        string? aiResponse = null;
        RoadmapRequestDTO? toolArguments = null;
        string? targetRole = null;
        string[]? followUpSuggestions = null;
        string? videoUrl = null;
        string? videoSearchQuery = null;
        AskUserQuestion[]? questions = null;
        string? coachMonologue = null;
        string? chatTitle = null;
        string? thinkingTitle = null;

        if (!skipIntentDetection)
        {
            var detectionPrompt = string.Format(prompts.IntentDetectionUserPromptTemplate,
                (!string.IsNullOrEmpty(conversationContextText) ? $"Conversation History:\n{conversationContextText}\n\n" : ""),
                query,
                profileSummary,
                fetchedMarketInsight?.TopRequiredSkills is { Length: > 0 }
                    ? string.Join(", ", fetchedMarketInsight.TopRequiredSkills)
                    : "No market skills data available");

            var detectionJsonBuilder = new StringBuilder();
            string detectionModel = "Unknown";
            string thinkingLog = "";
            bool titleExtracted = false;

            await foreach (var chunk in customerHelper.SendStreamRequestByTask(detectionPrompt, LLMInteractionType.ChatWithAI, useThinking: true, cancellationToken: cancellationToken, userId: userId, isSuperAdmin: IsSuperAdmin(), chatMode: chatMode, systemPrompt: systemPrompt))
            {
                if (chunk.StartsWith("__FALLBACK__:"))
                {
                    var provider = chunk[13..].Trim();
                    yield return FormatSse("fallback", new { provider, message = "Switched to backup AI model" });
                }
                else if (chunk == "__RETRY__")
                {
                    yield return FormatSse("retry", new { message = "Rate limited, retrying with backup model..." });
                    detectionJsonBuilder.Clear();
                    thinkingLog = "";
                    thinkingTitle = null;
                    titleExtracted = false;
                }
                else if (chunk.StartsWith("__MODEL__:"))
                {
                    detectionModel = chunk[10..].Trim();
                    yield return FormatSse("model", new { name = detectionModel, reasoning = true });
                }
                else if (chunk.StartsWith("__THINKING_CONTENT__:"))
                {
                    var content = chunk[21..];
                    thinkingLog += content;

                    // Only emit title on first chunk to start the "Thought for Xs" timer
                    // Raw thinking stays internal — coach_monologue will be sent after JSON parse
                    if (!titleExtracted)
                    {
                        titleExtracted = true;
                        thinkingTitle = "Analyzing your question";
                        yield return FormatSse("thinking", new { title = thinkingTitle, debug = "" });
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

            interactionType = parsed?.intent ?? LLMInteractionType.ChatWithAI;
            confidence = parsed?.confidence ?? 0;
            aiResponse = parsed?.response;
            toolArguments = parsed?.toolArguments;
            targetRole = parsed?.targetRole;
            followUpSuggestions = parsed?.followUpSuggestions;
            videoUrl = parsed?.videoUrl;
            videoSearchQuery = parsed?.videoSearchQuery;
            questions = parsed?.questions;
            coachMonologue = parsed?.coachMonologue;
            chatTitle = parsed?.title;
        }

        // Emit coach_monologue as the user-facing thinking content
        if (!string.IsNullOrEmpty(coachMonologue))
        {
            yield return FormatSse("thinking", new { title = thinkingTitle ?? "Analyzing your question", debug = coachMonologue });
        }

        // Emit AI-generated title for the conversation sidebar
        if (!string.IsNullOrEmpty(chatTitle))
        {
            yield return FormatSse("title", new { title = chatTitle });

            // Persist title to database so it survives page reload
            try
            {
                await chatRepository.AddMessageAsync(new ChatMessageEntity
                {
                    UserId = userId,
                    SessionId = actualSessionId,
                    Message = chatTitle,
                    Role = "title",
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist chat title for session {SessionId}", actualSessionId);
            }
        }

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

        // ── Change F (continued): Apply UI forced intent override ──
        if (uiForcedIntent.HasValue && interactionType != uiForcedIntent.Value)
        {
            _logger.LogDebug("Overriding LLM intent {LlmIntent} → {ForcedIntent} from UI mode selection",
                interactionType, uiForcedIntent.Value);
            interactionType = uiForcedIntent.Value;
            confidence = Math.Max(confidence, 80);
        }

        yield return FormatSse("intent", new
        {
            name = interactionType.ToString(),
            confidence,
            tags = toolArguments?.Tags,
            targetRole,
        });

        // Explicit UI mode wins over stale roadmap DB state; reset to idle to avoid next-turn contamination.
        var uiOverridesRoadmapState = uiForcedIntent.HasValue
            && uiForcedIntent.Value != LLMInteractionType.RoadmapGeneration;

        if (uiOverridesRoadmapState && (hasAskedDiscovery || hasPendingRoadmapConfirmation))
        {
            _logger.LogInformation(
                "UI mode {Mode} overrides stale roadmap state {State} for {UserId} — resetting to idle",
                canonicalUiMode, roadmapFlowState, userId);
            await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
        }

        var forceRoadmapFlow = !uiOverridesRoadmapState
            && (roadmapConfirmationReply || roadmapStateFollowUp || roadmapRefinementReply);

        if (forceRoadmapFlow && interactionType != LLMInteractionType.RoadmapGeneration)
        {
            interactionType = LLMInteractionType.RoadmapGeneration;
            confidence = Math.Max(confidence, 75);
        }

        if (interactionType == LLMInteractionType.RoadmapGeneration && toolArguments == null)
        {
            toolArguments = RoadmapIntentHelper.BuildRoadmapFallbackRequest(query, conversationContextText);
        }

        // Requires confirmed profile before proceeding; re-asks discovery if fields are still missing.
        var roadmapNeedsConfirmation = interactionType == LLMInteractionType.RoadmapGeneration
            && !roadmapConfirmationReply
            && !roadmapRefinementReply;

        // Re-fetch to pick up fields persisted by TryPersistProfileFromLatestMessageAsync this turn.
        if (interactionType == LLMInteractionType.RoadmapGeneration && !hasSufficientRoadmapProfile)
        {
            try
            {
                var refreshedProfile = await userProfileRepository.GetByUserIdAsync(userId);
                if (refreshedProfile != null)
                {
                    userProfile = refreshedProfile;
                    hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);
                    profileSummary = userProfile.ToPromptSummary();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to re-fetch profile during roadmap flow for {UserId}", userId);
            }

            // Fallback: recover roadmap profile fields from user turns already present in
            // session context. This prevents repeated discovery loops when profile storage
            // is temporarily unavailable.
            if (!hasSufficientRoadmapProfile)
            {
                try
                {
                    var contextDerivedProfile = ExtractProfileFromConversationContext(userId, conversationContextText);
                    if (contextDerivedProfile != null)
                    {
                        if (userProfile is null)
                        {
                            userProfile = contextDerivedProfile;
                        }
                        else
                        {
                            userProfile.MergeFrom(contextDerivedProfile);
                        }

                        hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);
                        profileSummary = userProfile.ToPromptSummary();

                        try
                        {
                            await userProfileRepository.UpsertAsync(userProfile);
                        }
                        catch (Exception persistEx)
                        {
                            _logger.LogDebug(persistEx,
                                "Context-derived profile persistence failed for {UserId}; continuing with in-memory merge",
                                userId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "Failed to derive profile from conversation context for {UserId}",
                        userId);
                }
            }

            // Only run the expensive AI extractor if the static parser failed to gather a sufficient profile.
            // This happens when users type free-form text instead of using the AskUserBlock UI.
            if (!hasSufficientRoadmapProfile && hasAskedDiscovery)
            {
                try
                {
                    var contextForExtraction = $"{conversationContextText}\n[user]: {query}";
                    var aiExtracted = await customerHelper.ExtractProfileAsync(contextForExtraction);
                    if (aiExtracted != null)
                    {
                        var aiProfileEntity = UserProfileEntity.FromExtraction(userId, aiExtracted);
                        await userProfileRepository.UpsertAsync(aiProfileEntity);
                        _logger.LogInformation("Synchronous AI profile extraction succeeded for {UserId} during discovery", userId);

                        // Re-fetch one final time after AI extraction
                        var finalProfile = await userProfileRepository.GetByUserIdAsync(userId);
                        if (finalProfile != null)
                        {
                            userProfile = finalProfile;
                            hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);
                            profileSummary = userProfile.ToPromptSummary();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Synchronous AI profile extraction failed for {UserId}, continuing with static extraction", userId);
                }
            }
        }

        if (roadmapNeedsConfirmation)
        {
            // Profile gate: incomplete profile asks discovery unless max attempts reached.
            if (!hasSufficientRoadmapProfile)
            {
                // Hard exit: after max attempts, accept partial profile and proceed to confirmation
                // to prevent an infinite ask_user loop when the user's answers don't populate the profile.
                if (discoveryAttemptCount >= 2)
                {
                    _logger.LogInformation(
                        "Max discovery attempts ({Count}) reached for {UserId} — proceeding with partial profile",
                        discoveryAttemptCount, userId);
                    // Fall through to confirmation below
                }
                else
                {
                    var discoveryQuestions = (questions is { Length: > 0 } && IsRoadmapIntakeQuestionSet(questions))
                        ? questions
                        : BuildRoadmapDiscoveryQuestions(userProfile, query, conversationContextText);
                    discoveryQuestions = DeduplicateQuestions(discoveryQuestions, previouslyAskedIds);

                    var discoveryIntro = !string.IsNullOrWhiteSpace(aiResponse)
                        ? aiResponse
                        : "Before I generate a personalized roadmap, I need a bit more about your background and goals.";

                    yield return FormatSse("text", new { delta = discoveryIntro });
                    if (discoveryQuestions.Length > 0)
                    {
                        yield return FormatSse("ask_user", new { questions = discoveryQuestions });
                        MarkQuestionsAsked(previouslyAskedIds, discoveryQuestions);
                        await conversationContextManager.AddMessageToContextAsync(
                            userId, actualSessionId,
                            $"{discoveryIntro}\n[ASKED_QUESTIONS:{string.Join(",", discoveryQuestions.Select(q => q.Id))}]",
                            "assistant");
                    }
                    else
                    {
                        await conversationContextManager.AddMessageToContextAsync(
                            userId, actualSessionId, discoveryIntro, "assistant");
                    }

                    yield return FormatSse("done", new { message = "Roadmap discovery questions requested" });

                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateDiscoveryAsked);

                    yield break;
                }
            }

            // Profile is sufficient (or max attempts exhausted) → ask for confirmation
            if (!string.IsNullOrEmpty(aiResponse))
                yield return FormatSse("text", new { delta = aiResponse });

            var confirmQuestions = (questions is { Length: > 0 } && !IsRoadmapIntakeQuestionSet(questions))
                ? questions
                : BuildRoadmapConfirmationQuestions();
            confirmQuestions = DeduplicateQuestions(confirmQuestions, previouslyAskedIds);

            if (confirmQuestions.Length > 0)
            {
                yield return FormatSse("ask_user", new { questions = confirmQuestions });
                MarkQuestionsAsked(previouslyAskedIds, confirmQuestions);
            }

            yield return FormatSse("done", new { message = "Roadmap confirmation requested" });

            await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStatePendingConfirmation);

            await conversationContextManager.AddMessageToContextAsync(
                userId,
                actualSessionId,
                $"{aiResponse ?? "Please confirm if you want me to generate your roadmap now."}\n[ASKED_QUESTIONS:{string.Join(",", confirmQuestions.Select(q => q.Id))}]",
                "assistant");

            yield break;
        }

        // Safety net: incomplete profile should not reach the pipeline, unless max discovery attempts have been exhausted.
        // Respecting the max-attempts cap here prevents a second infinite-loop entry point.
        if (interactionType == LLMInteractionType.RoadmapGeneration && !hasSufficientRoadmapProfile)
        {
            var safetyAttemptCount = discoveryAttemptCount;
            if (safetyAttemptCount >= 2)
            {
                // Max attempts reached — let the pipeline proceed with the partial profile.
                _logger.LogInformation(
                    "Safety net: max discovery attempts ({Count}) exhausted for {UserId} — allowing pipeline to proceed",
                    safetyAttemptCount, userId);
            }
            else
            {
                _logger.LogInformation(
                    "Pre-pipeline safety net: profile still incomplete for {UserId} (attempt {Count}), re-entering discovery",
                    userId, safetyAttemptCount + 1);

                var safetyQuestions = DeduplicateQuestions(
                    BuildRoadmapDiscoveryQuestions(userProfile, query, conversationContextText),
                    previouslyAskedIds);
                var safetyIntro = "I'd like to make sure your roadmap is personalized. Could you help me with a couple more details?";

                yield return FormatSse("text", new { delta = safetyIntro });
                if (safetyQuestions.Length > 0)
                {
                    yield return FormatSse("ask_user", new { questions = safetyQuestions });
                    MarkQuestionsAsked(previouslyAskedIds, safetyQuestions);
                }
                yield return FormatSse("done", new { message = "Roadmap discovery re-requested (safety net)" });

                await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateDiscoveryAsked);
                await conversationContextManager.AddMessageToContextAsync(
                    userId, actualSessionId,
                    $"{safetyIntro}\n[ASKED_QUESTIONS:{string.Join(",", safetyQuestions.Select(q => q.Id))}]",
                    "assistant");

                yield break;
            }
        }

        var shouldEmitQuestions = questions is { Length: > 0 }
            && !(interactionType == LLMInteractionType.RoadmapGeneration
                 && (roadmapConfirmationReply || roadmapRefinementReply));

        if (shouldEmitQuestions)
        {
            var dedupedQuestions = DeduplicateQuestions(questions, previouslyAskedIds);

            // Emit the response text first (introduces the questions)
            if (!string.IsNullOrEmpty(aiResponse))
                yield return FormatSse("text", new { delta = aiResponse });

            if (dedupedQuestions.Length > 0)
            {
                yield return FormatSse("ask_user", new { questions = dedupedQuestions });
                MarkQuestionsAsked(previouslyAskedIds, dedupedQuestions);
            }

            yield return FormatSse("done", new { message = "Questions sent" });

            // Save conversation context
            await conversationContextManager.AddMessageToContextAsync(
                userId, actualSessionId,
                $"{aiResponse ?? "Asked clarification questions"}\n[ASKED_QUESTIONS:{string.Join(",", dedupedQuestions.Select(q => q.Id))}]",
                "assistant");

            yield break;
        }

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

                if (!string.IsNullOrEmpty(aiResponse))
                    yield return FormatSse("text", new { delta = aiResponse });

                yield return FormatSse("video", new { url = resolvedVideoUrl, message = previewMessage });

                var contextMessage = !string.IsNullOrEmpty(aiResponse)
                    ? $"{aiResponse}\n[Video: {resolvedVideoUrl}]"
                    : $"{previewMessage}\n[Video: {resolvedVideoUrl}]";
                await conversationContextManager
                    .AddMessageToContextAsync(userId, actualSessionId, contextMessage, "assistant");
            }
            else
            {
                yield return FormatSse("tool", new { name = "VideoSearch", state = "end", summary = "No embeddable video found" });
                interactionType = LLMInteractionType.ChatWithAI;
            }
        }

        if (interactionType == LLMInteractionType.ChatWithAI)
        {
            var (chatSystemPrompt, chatPrompt) = BuildChatPrompt(conversationContext.ToString(), query, profileSummary, chatMode, detectedLanguage);

            var fullResponseBuilder = new StringBuilder();
            var chatThinkingLog = "";
            var chatThinkingTitle = "";
            var chatTitleExtracted = false;

            try
            {
                await foreach (var chunk in customerHelper.SendStreamRequestByTask(chatPrompt, LLMInteractionType.ChatWithAI, useThinking: true, cancellationToken: cancellationToken, userId: userId, isSuperAdmin: IsSuperAdmin(), chatMode: chatMode, systemPrompt: chatSystemPrompt))
                {
                    if (chunk.StartsWith("__FALLBACK__:"))
                    {
                        var provider = chunk[13..].Trim();
                        yield return FormatSse("fallback", new { provider, message = "Switched to backup AI model" });
                    }
                    else if (chunk == "__RETRY__")
                    {
                        yield return FormatSse("retry", new { message = "Rate limited, retrying with backup model..." });
                        fullResponseBuilder.Clear();
                    }
                    else if (chunk.StartsWith("__MODEL__:"))
                    {
                        yield return FormatSse("model", new { name = chunk[10..].Trim(), reasoning = true });
                    }
                    else if (chunk.StartsWith("__THINKING_CONTENT__:"))
                    {
                        var content = chunk[21..];
                        chatThinkingLog += content;

                        // Emit clean title on first chunk, then stream debug incrementally
                        if (!chatTitleExtracted)
                        {
                            chatTitleExtracted = true;
                            chatThinkingTitle = "Crafting response";
                            yield return FormatSse("thinking", new { title = chatThinkingTitle, debug = content });
                        }
                        else
                        {
                            yield return FormatSse("thinking", new { title = chatThinkingTitle, debug = content });
                        }
                    }
                    else if (!chunk.StartsWith("__STATUS__"))
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
        else if (interactionType != LLMInteractionType.ChatWithAI)
        {
            string finalResponse = "";

            if ((interactionType == LLMInteractionType.RoadmapGeneration || interactionType == LLMInteractionType.CVAnalysis) && toolArguments != null)
            {
                // Validate LLM-generated tags against market data
                if (toolArguments.Tags is { Length: > 0 } && fetchedMarketInsight?.TopRequiredSkills is { Length: > 0 })
                {
                    toolArguments.Tags = ValidateTagsAgainstMarket(
                        toolArguments.Tags, fetchedMarketInsight.TopRequiredSkills, interactionType);
                }

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

                // Keep SSE connection alive during long-running pipeline calls.
                string apiResponse = "";
                bool pipelineSuccess = false;
                string? pipelineError = null;
                var apiResponseTask = ExecuteRoadmapGenerationAsync(toolArguments!, cancellationToken);
                var heartbeatSeconds = 0;
                while (!apiResponseTask.IsCompleted)
                {
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
                    var completed = await Task.WhenAny(apiResponseTask, delayTask);
                    if (completed == apiResponseTask)
                        break;

                    if (cancellationToken.IsCancellationRequested || delayTask.IsCanceled)
                        throw new OperationCanceledException(cancellationToken);

                    heartbeatSeconds += 8;
                    yield return FormatSse("tool", new
                    {
                        name = "RoadmapGenerator",
                        state = "processing",
                        summary = $"Roadmap generation in progress... {heartbeatSeconds}s"
                    });
                }

                try
                {
                    apiResponse = await apiResponseTask;
                    pipelineSuccess = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Roadmap generation failed for user {UserId} in session {SessionId}",
                        userId,
                        actualSessionId);
                    pipelineError = "I apologize, but I encountered an error while generating your roadmap. Please try again.";
                }

                if (!pipelineSuccess && pipelineError != null)
                {
                    yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "end", summary = "Generation failed" });

                    yield return FormatSse("result", new { status = "error", message = pipelineError });
                    yield return FormatSse("text", new { delta = $"\n\n{pipelineError}" });

                    finalResponse = pipelineError;
                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
                }

                if (pipelineSuccess)
                {
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

                    // Inject generationId into result data so it's stored in Cosmos and sent via SSE.
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

                    // Persist parseable roadmap payload for refresh hydration.
                    finalResponse = $"[SYSTEM] {resultJsonString}";

                    yield return FormatSse("tool", new { name = "RoadmapGenerator", state = "end", summary = "Roadmap generated" });

                    // Suppress pre-decision aiResponse when forceRoadmapFlow overrode intent.
                    if (!string.IsNullOrEmpty(aiResponse) && !forceRoadmapFlow)
                    {
                        yield return FormatSse("text", new { delta = aiResponse });
                        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, aiResponse, "assistant");
                    }

                    yield return FormatSse("result", new { status = "success", data = resultData });

                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateCompleted);
                }
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

        if (options.CurrentValue.Llm.EnableBackgroundProfileExtraction
            && (classifiedMode == "FRIEND"
                || interactionType == LLMInteractionType.RoadmapGeneration
                || hasAskedDiscovery
                || hasPendingRoadmapConfirmation))
        {
            var capturedUserId = userId;
            var capturedSessionId = actualSessionId;
            var capturedContextManager = conversationContextManager;
            var capturedHelper = customerHelper;
            var capturedProfileRepo = userProfileRepository;
            var capturedLogger = _logger;

            _ = Task.Run(async () => // fire-and-forget; don't capture 'this'
            {
                try
                {
                    var context = await capturedContextManager.GetConversationContextAsync(capturedUserId, capturedSessionId);
                    var extractedProfile = await capturedHelper.ExtractProfileAsync(context);
                    if (extractedProfile != null)
                    {
                        capturedLogger.LogDebug("Extracted profile for user {UserId}: Interests={Interests}, TargetRole={TargetRole}",
                            capturedUserId,
                            extractedProfile.Interests != null ? string.Join(", ", extractedProfile.Interests) : "none",
                            extractedProfile.TargetRole ?? "none");

                        // Save to Cosmos DB with smart merge
                        var profileEntity = UserProfileEntity.FromExtraction(capturedUserId, extractedProfile);
                        await capturedProfileRepo.UpsertAsync(profileEntity);

                        capturedLogger.LogInformation("Saved profile to Cosmos DB for user {UserId}", capturedUserId);
                    }
                }
                catch (Exception ex)
                {
                    capturedLogger.LogWarning(ex, "Background profile extraction failed for user {UserId}", capturedUserId);
                }
            });
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

            // Strip [Active Mode: X] prefix; extract mode for intent routing.
            string? uiSelectedMode = null;
            LLMInteractionType? uiForcedIntent = null;
            var nonStreamModeMatch = System.Text.RegularExpressions.Regex.Match(query, @"^\[Active Mode:\s*(.+?)\]\s*\n?");
            if (nonStreamModeMatch.Success)
            {
                uiSelectedMode = nonStreamModeMatch.Groups[1].Value.Trim();
                query = query[nonStreamModeMatch.Length..].TrimStart();
                _logger.LogDebug("UI mode override detected in non-streaming: {Mode}", uiSelectedMode);

                var modeLower = uiSelectedMode.ToLowerInvariant();
                if (modeLower.Contains("roadmap"))
                {
                    uiForcedIntent = LLMInteractionType.RoadmapGeneration;
                    _logger.LogDebug("UI mode override: Roadmap Builder → forcing RoadmapGeneration intent");
                }
                else if (modeLower.Contains("cv") || modeLower.Contains("resume"))
                {
                    uiForcedIntent = LLMInteractionType.CVAnalysis;
                    _logger.LogDebug("UI mode override: CV Analyzer → forcing CVAnalysis intent");
                }
                else if (modeLower.Contains("career") || modeLower.Contains("coach"))
                {
                    uiForcedIntent = LLMInteractionType.ChatWithAI;
                    _logger.LogDebug("UI mode override: Career Coach → forcing ChatWithAI intent");
                }
            }

            await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId, query, "user");
            await TryPersistProfileFromLatestMessageAsync(userId, query);

            var contextTask = conversationContextManager.GetConversationContextAsync(userId, actualSessionId);
            var profileTask = userProfileRepository.GetByUserIdAsync(userId);
            await Task.WhenAll(contextTask, profileTask);

            var conversationContext = await contextTask;
            var conversationContextText = conversationContext.ToString();
            var previouslyAskedIds = ExtractAskedQuestionIds(conversationContextText);
            var userProfile = await profileTask;
            var profileSummary = userProfile?.ToPromptSummary() ?? "No profile data yet";
            var hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);

            var (interactionType, confidence, aiResponse, toolArguments, _, thinkingContent, _, _, videoUrl, videoSearchQuery, questions) = await DetectIntentAsync(query, conversationContextText, profileSummary);

            if (uiForcedIntent.HasValue && interactionType != uiForcedIntent.Value)
            {
                _logger.LogDebug("Overriding LLM intent {LlmIntent} → {ForcedIntent} from UI mode selection",
                    interactionType, uiForcedIntent.Value);
                interactionType = uiForcedIntent.Value;
                confidence = Math.Max(confidence, 80);
            }

            var roadmapFlowState = await GetRoadmapFlowStateAsync(userId, actualSessionId);
            if (roadmapFlowState == RoadmapIntentHelper.RoadmapStateCompleted)
            {
                await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
                roadmapFlowState = RoadmapIntentHelper.RoadmapStateIdle;
            }
            var hasPendingRoadmapConfirmation = roadmapFlowState == RoadmapIntentHelper.RoadmapStatePendingConfirmation;
            var hasAskedDiscovery = roadmapFlowState == RoadmapIntentHelper.RoadmapStateDiscoveryAsked;

            var explicitRoadmapRequest = IsExplicitRoadmapRequest(query);
            var hasRoadmapContext = hasPendingRoadmapConfirmation || hasAskedDiscovery || RoadmapIntentHelper.HasRoadmapContext(conversationContextText);
            var roadmapDeclineReply = HasRoadmapDecline(query) && hasRoadmapContext;
            var roadmapConfirmationReply = HasRoadmapConfirmation(query) && hasRoadmapContext;
            var roadmapStateFollowUp = (hasPendingRoadmapConfirmation || hasAskedDiscovery)
                && !roadmapDeclineReply
                && !roadmapConfirmationReply
                && !explicitRoadmapRequest;
            var roadmapRefinementReply = hasRoadmapContext
                && !explicitRoadmapRequest
                && !roadmapDeclineReply
                && !roadmapConfirmationReply
                && await IsRoadmapRefinementSemanticAsync(query, conversationContextText, CancellationToken.None);

            // Explicit UI mode wins over stale roadmap state; reset to idle.
            var uiOverridesRoadmapState = uiForcedIntent.HasValue
                && uiForcedIntent.Value != LLMInteractionType.RoadmapGeneration;

            if (uiOverridesRoadmapState && (hasAskedDiscovery || hasPendingRoadmapConfirmation))
            {
                _logger.LogInformation(
                    "UI mode {Mode} overrides stale roadmap state {State} for {UserId} — resetting to idle",
                    uiForcedIntent, roadmapFlowState, userId);
                await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);

                hasAskedDiscovery = false;
                hasPendingRoadmapConfirmation = false;
                roadmapStateFollowUp = false;
            }

            var forceRoadmapFlow = !uiOverridesRoadmapState
                && (roadmapConfirmationReply || roadmapStateFollowUp || roadmapRefinementReply);

            if (roadmapDeclineReply)
            {
                await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);
            }

            if (forceRoadmapFlow && interactionType != LLMInteractionType.RoadmapGeneration)
            {
                interactionType = LLMInteractionType.RoadmapGeneration;
                confidence = Math.Max(confidence, 75);
            }

            if (interactionType == LLMInteractionType.RoadmapGeneration && toolArguments == null)
            {
                toolArguments = RoadmapIntentHelper.BuildRoadmapFallbackRequest(query, conversationContextText);
            }
            // When the user is answering discovery questions, run AI-powered profile extraction
            // synchronously. The static parser may miss free-form answers.
            if (interactionType == LLMInteractionType.RoadmapGeneration && !hasSufficientRoadmapProfile)
            {
                try
                {
                    var refreshedProfile = await userProfileRepository.GetByUserIdAsync(userId);
                    if (refreshedProfile != null)
                    {
                        userProfile = refreshedProfile;
                        hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);
                        profileSummary = userProfile.ToPromptSummary();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to re-fetch profile during roadmap flow for {UserId}", userId);
                }

                // Only run the expensive AI extractor if the static parser failed to gather a sufficient profile.
                if (!hasSufficientRoadmapProfile && hasAskedDiscovery)
                {
                    try
                    {
                        var contextForExtraction = $"{conversationContextText}\n[user]: {query}";
                        var aiExtracted = await customerHelper.ExtractProfileAsync(contextForExtraction);
                        if (aiExtracted != null)
                        {
                            var aiProfileEntity = UserProfileEntity.FromExtraction(userId, aiExtracted);
                            await userProfileRepository.UpsertAsync(aiProfileEntity);
                            _logger.LogDebug("Synchronous AI profile extraction succeeded for {UserId} during discovery (non-streaming)", userId);

                            // Re-fetch one final time after AI extraction
                            var finalProfile = await userProfileRepository.GetByUserIdAsync(userId);
                            if (finalProfile != null)
                            {
                                userProfile = finalProfile;
                                hasSufficientRoadmapProfile = HasSufficientRoadmapProfile(userProfile);
                                profileSummary = userProfile.ToPromptSummary();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Synchronous AI profile extraction failed for {UserId} (non-streaming), continuing with static extraction", userId);
                    }
                }
            }

            var roadmapNeedsConfirmation = interactionType == LLMInteractionType.RoadmapGeneration
                && !roadmapConfirmationReply
                && !roadmapRefinementReply;

            var discoveryAttempts = await GetDiscoveryAttemptCountAsync(userId, actualSessionId);

            if (roadmapNeedsConfirmation)
            {
                // Profile gate: incomplete profile asks discovery unless max attempts reached.
                if (!hasSufficientRoadmapProfile)
                {
                    // Hard exit: after max attempts, proceed to confirmation to avoid an infinite ask_user loop.
                    if (discoveryAttempts >= 2)
                    {
                        _logger.LogInformation(
                            "Max discovery attempts ({Count}) reached for {UserId} in non-streaming path — proceeding with partial profile",
                            discoveryAttempts, userId);
                        // Fall through to confirmation below
                    }
                    else
                    {
                        var responseMessage = !string.IsNullOrEmpty(aiResponse)
                            ? aiResponse
                            : "Before I generate a personalized roadmap, I need a bit more about your background and goals.";

                        var responseQuestions = DeduplicateQuestions(
                            (questions is { Length: > 0 } && IsRoadmapIntakeQuestionSet(questions))
                                ? questions
                                : BuildRoadmapDiscoveryQuestions(userProfile, query, conversationContextText),
                            previouslyAskedIds);

                        await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId,
                            $"{responseMessage}\n[ASKED_QUESTIONS:{string.Join(",", responseQuestions.Select(q => q.Id))}]", "assistant");
                        await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateDiscoveryAsked);

                        return new APIResponse<AiIntentDetectionResponse>()
                        {
                            StatusCode = HttpStatusCode.OK,
                            Data = new AiIntentDetectionResponse
                            {
                                InteractionType = LLMInteractionType.RoadmapGeneration,
                                Confidence = confidence,
                                Success = true,
                                Answer = responseMessage,
                                Thinking = !string.IsNullOrEmpty(thinkingContent),
                                ThinkingLog = thinkingContent,
                                Questions = responseQuestions
                            }
                        };
                    }
                }

                // Profile is sufficient (or max attempts exhausted) → ask for confirmation
                var confirmMessage = !string.IsNullOrEmpty(aiResponse)
                    ? aiResponse
                    : "I can generate your roadmap. Please confirm to continue.";

                var confirmQuestions = DeduplicateQuestions(
                    (questions is { Length: > 0 } && !IsRoadmapIntakeQuestionSet(questions))
                        ? questions
                        : BuildRoadmapConfirmationQuestions(),
                    previouslyAskedIds);

                await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId,
                    $"{confirmMessage}\n[ASKED_QUESTIONS:{string.Join(",", confirmQuestions.Select(q => q.Id))}]", "assistant");
                await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStatePendingConfirmation);

                return new APIResponse<AiIntentDetectionResponse>()
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new AiIntentDetectionResponse
                    {
                        InteractionType = LLMInteractionType.RoadmapGeneration,
                        Confidence = confidence,
                        Success = true,
                        Answer = confirmMessage,
                        Thinking = !string.IsNullOrEmpty(thinkingContent),
                        ThinkingLog = thinkingContent,
                        Questions = confirmQuestions
                    }
                };
            }

            // Safety net: incomplete profile should not reach the pipeline, unless max discovery attempts are exhausted.
            if (interactionType == LLMInteractionType.RoadmapGeneration && !hasSufficientRoadmapProfile)
            {
                var safetyAttempts = await GetDiscoveryAttemptCountAsync(userId, actualSessionId);
                if (safetyAttempts >= 2)
                {
                    // Max attempts exhausted — let the pipeline proceed with partial profile data.
                    _logger.LogInformation(
                        "Safety net (non-streaming): max discovery attempts ({Count}) exhausted for {UserId} — allowing pipeline to proceed",
                        safetyAttempts, userId);
                }
                else
                {
                    _logger.LogInformation(
                        "Safety net (non-streaming): profile still incomplete for {UserId} (attempt {Count}), re-entering discovery",
                        userId, safetyAttempts + 1);

                    var safetyMessage = "I'd like to make sure your roadmap is personalized. Could you help me with a couple more details?";
                    var safetyQuestions = DeduplicateQuestions(
                        BuildRoadmapDiscoveryQuestions(userProfile, query, conversationContextText),
                        previouslyAskedIds);

                    await conversationContextManager.AddMessageToContextAsync(userId, actualSessionId,
                        $"{safetyMessage}\n[ASKED_QUESTIONS:{string.Join(",", safetyQuestions.Select(q => q.Id))}]", "assistant");
                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateDiscoveryAsked);

                    return new APIResponse<AiIntentDetectionResponse>()
                    {
                        StatusCode = HttpStatusCode.OK,
                        Data = new AiIntentDetectionResponse
                        {
                            InteractionType = LLMInteractionType.RoadmapGeneration,
                            Confidence = confidence,
                            Success = true,
                            Answer = safetyMessage,
                            Thinking = !string.IsNullOrEmpty(thinkingContent),
                            ThinkingLog = thinkingContent,
                            Questions = safetyQuestions
                        }
                    };
                }
            }

            if ((interactionType == LLMInteractionType.RoadmapGeneration || interactionType == LLMInteractionType.CVAnalysis) && toolArguments != null)
            {
                toolArguments.JobId = Guid.NewGuid().ToString("N")[..12];
                string apiResponse = "";
                try
                {
                    apiResponse = await ExecuteRoadmapGenerationAsync(toolArguments);

                    // Suppress pre-decision aiResponse when forceRoadmapFlow overrode intent.
                    if (forceRoadmapFlow)
                    {
                        aiResponse = "Roadmap generated successfully.";
                    }
                    else
                    {
                        aiResponse = string.IsNullOrEmpty(aiResponse)
                            ? "Roadmap generated successfully."
                            : $"{aiResponse}\n\nRoadmap generated successfully.";
                    }

                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateCompleted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Roadmap generation failed for user {UserId} in session {SessionId} (non-streaming path)",
                        userId,
                        actualSessionId);

                    var errorMsg = "I apologize, but I encountered an error while generating your roadmap. Please try again.";
                    aiResponse = errorMsg;
                    await SaveRoadmapFlowStateAsync(userId, actualSessionId, RoadmapIntentHelper.RoadmapStateIdle);

                    return new APIResponse<AiIntentDetectionResponse>()
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Data = new AiIntentDetectionResponse
                        {
                            InteractionType = interactionType,
                            Confidence = confidence,
                            Success = false,
                            Answer = aiResponse,
                            ErrorMessage = errorMsg
                        }
                    };
                }
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
                VideoUrl = interactionType == LLMInteractionType.TopicPreview ? videoUrl : null,
                Questions = null
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
            _logger.LogError(ex, "Failed to process AI intent detection for user {UserId} in session {SessionId}", userId, sessionId);
            return new APIResponse<AiIntentDetectionResponse>()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Data = new AiIntentDetectionResponse
                {
                    InteractionType = LLMInteractionType.ChatWithAI,
                    Success = false,
                    ErrorMessage = "Unable to process your request right now. Please try again."
                }
            };
        }
    }

    private async Task<(LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments, string ModelName, string? ThinkingContent, string? targetRole, string[]? followUpSuggestions, string? videoUrl, string? videoSearchQuery, AskUserQuestion[]? questions)> DetectIntentAsync(string query, string conversationContext, string? profileSummary = null, ChatMode chatMode = ChatMode.Fast, string? marketSkillsList = null)
    {
        var prompts = options.CurrentValue.Prompts;
        var detectionPrompt = string.Format(prompts.IntentDetectionUserPromptTemplate,
            (!string.IsNullOrEmpty(conversationContext) ? $"Conversation History:\n{conversationContext}\n\n" : ""),
            query,
            profileSummary ?? "No profile data yet",
            marketSkillsList ?? "No market skills data available");

        var intentSystemPrompt = prompts.IntentDetectionSystemPrompt;
        if (chatMode == ChatMode.Deep)
            intentSystemPrompt = "Use your thinking capability to reason step-by-step before classifying the user's intent.\n\n" + intentSystemPrompt;
        if (!string.IsNullOrEmpty(prompts.LanguageRules))
            intentSystemPrompt = intentSystemPrompt.Replace("{LANGUAGE_RULES}", prompts.LanguageRules);

        try
        {
            var (responseText, modelName, thinkingContent) = await customerHelper.SendRequestByTask(
                detectionPrompt, LLMInteractionType.ChatWithAI, useThinking: true,
                chatMode: chatMode, systemPrompt: intentSystemPrompt);
            var parsed = ParseCombinedResponse(responseText);
            if (parsed.HasValue)
                return (parsed.Value.intent, parsed.Value.confidence, parsed.Value.response, parsed.Value.toolArguments, modelName, thinkingContent, parsed.Value.targetRole, parsed.Value.followUpSuggestions, parsed.Value.videoUrl, parsed.Value.videoSearchQuery, parsed.Value.questions);

            return (LLMInteractionType.ChatWithAI, 0, null, null, modelName, thinkingContent, null, null, null, null, null);
        }
        catch
        {
            return (LLMInteractionType.ChatWithAI, 0, null, null, "Unknown", null, null, null, null, null, null);
        }
    }

    private (string SystemPrompt, string UserPrompt) BuildChatPrompt(string context, string query, string? profileSummary = null, ChatMode chatMode = ChatMode.Fast, string? detectedLanguage = null)
    {
        var prompts = options.CurrentValue.Prompts;

        var enrichedContext = context;
        if (!string.IsNullOrEmpty(profileSummary) && profileSummary != "No profile data yet")
        {
            enrichedContext = $"[User Profile: {profileSummary}]\n\n{context}";
        }

        var chatSystemPrompt = chatMode == ChatMode.Deep
            && !string.IsNullOrEmpty(prompts.ChatSystemPromptDeep)
            ? prompts.ChatSystemPromptDeep
            : prompts.ChatSystemPrompt;

        // Inject shared language rules
        if (!string.IsNullOrEmpty(prompts.LanguageRules))
            chatSystemPrompt = chatSystemPrompt.Replace("{LANGUAGE_RULES}", prompts.LanguageRules);

        if (!string.IsNullOrEmpty(detectedLanguage))
        {
            var langName = detectedLanguage == "ar" ? "Arabic" : "English";
            chatSystemPrompt = $"[LANGUAGE CONSTRAINT: You MUST respond in {langName} only. This overrides all other language rules.]\n\n" + chatSystemPrompt;
        }

        var userPrompt = string.Format(prompts.ChatUserPromptTemplate,
            enrichedContext,
            query);
        return (chatSystemPrompt, userPrompt);
    }

    private (LLMInteractionType intent, float confidence, string? response, RoadmapRequestDTO? toolArguments, string? targetRole, string[]? followUpSuggestions, string? videoUrl, string? videoSearchQuery, AskUserQuestion[]? questions, string? coachMonologue, string? title)? ParseCombinedResponse(string? responseText)
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

            AskUserQuestion[]? questions = null;
            if (root.TryGetProperty("questions", out var qProp) && qProp.ValueKind == JsonValueKind.Array)
            {
                questions = JsonSerializer.Deserialize<AskUserQuestion[]>(qProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            var coachMonologue = root.TryGetProperty("coach_monologue", out var cmProp) && cmProp.ValueKind == JsonValueKind.String
                ? cmProp.GetString()
                : null;

            var title = root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
                ? titleProp.GetString()
                : null;

            if (!string.IsNullOrEmpty(intentStr) && Enum.TryParse<LLMInteractionType>(intentStr, true, out var intent))
            {
                return (intent, Math.Clamp(confidence, 0, 100), response, toolArguments, targetRole, followUpSuggestions, videoUrl, videoSearchQuery, questions, coachMonologue, title);
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
            var resultJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return resultJson;
            }
            else
            {
                throw new InvalidOperationException($"Error: The roadmap service returned {response.StatusCode}. Details: {resultJson}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException("Error: The roadmap service request timed out. Please try again.", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException
            && ex is not TimeoutException
            && ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Roadmap service call failed for request payload.");
            throw new Exception("Error: Unable to reach roadmap service right now. Please try again later.", ex);
        }
    }


    private string GenerateSessionId(string userId)
    {
        var input = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLower()[..16];
    }

    private static bool HasRoadmapConfirmation(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var normalized = query.Trim().ToLowerInvariant();

        var roadmapTerms = new[]
        {
            "roadmap",
            "learning path",
            "خريطة طريق",
            "مسار تعلم",
            "رودماب"
        };

        var confirmTerms = new[]
        {
            "yes",
            "ok",
            "okay",
            "confirm",
            "continue",
            "go on",
            "go ahead",
            "proceed",
            "generate now",
            "create now",
            "نعم",
            "أكيد",
            "تمام",
            "تابع",
            "ابدأ",
            "وافق"
        };

        var hasConfirmTerm = confirmTerms.Any(normalized.Contains);

        if (!hasConfirmTerm)
            return false;

        var hasRoadmapTerm = roadmapTerms.Any(normalized.Contains);
        return hasRoadmapTerm || normalized is "yes" or "نعم" or "أكيد" or "تمام";
    }

    private static bool HasRoadmapDecline(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var normalized = query.Trim().ToLowerInvariant();
        var declinePhrases = new[]
        {
            "not now",
            "cancel",
            "skip",
            "later",
            "don't",
            "do not",
            "مش دلوقتي",
            "الغاء",
            "إلغاء",
            "بعدين"
        };

        if (declinePhrases.Any(normalized.Contains))
            return true;

        var tokens = normalized
            .Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries);

        return tokens.Contains("no") || tokens.Contains("لا") || tokens.Contains("لأ");
    }

    private string[] ValidateTagsAgainstMarket(
        string[] llmTags, string[] marketSkills, LLMInteractionType interactionType)
    {
        var marketSet = new HashSet<string>(marketSkills, StringComparer.OrdinalIgnoreCase);

        // Count how many LLM tags overlap with market skills (fuzzy: check if market skill is contained in tag)
        int overlapCount = 0;
        foreach (var tag in llmTags)
        {
            if (marketSet.Any(ms =>
                tag.Contains(ms, StringComparison.OrdinalIgnoreCase) ||
                ms.Contains(tag, StringComparison.OrdinalIgnoreCase)))
            {
                overlapCount++;
            }
        }

        var overlapRatio = llmTags.Length > 0 ? (float)overlapCount / llmTags.Length : 0;

        // CVAnalysis: light validation — trust LLM gap analysis, only warn
        if (interactionType == LLMInteractionType.CVAnalysis)
        {
            if (overlapRatio < 0.3f)
            {
                _logger.LogWarning("CV analysis tags have low market overlap ({Ratio:P0}). LLM tags: [{Tags}], Market: [{Market}]",
                    overlapRatio, string.Join(", ", llmTags), string.Join(", ", marketSkills.Take(10)));
            }
            return llmTags; // Trust gap analysis
        }

        // RoadmapGeneration: if <50% overlap, replace with market-derived tags
        if (interactionType == LLMInteractionType.RoadmapGeneration && overlapRatio < 0.5f)
        {
            _logger.LogWarning("Roadmap tags replaced: low market overlap ({Ratio:P0}). Original: [{Tags}] → Market: [{Market}]",
                overlapRatio, string.Join(", ", llmTags), string.Join(", ", marketSkills.Take(10)));

            // Build tags from market skills, transforming to course-level names
            var marketTags = marketSkills
                .Take(Math.Min(10, Math.Max(6, marketSkills.Length)))
                .Select(NormalizeSkillToTag)
                .ToArray();

            return marketTags;
        }

        return llmTags;
    }

    private static string NormalizeSkillToTag(string skill)
    {
        // Transform raw skill names into searchable course-level titles
        var normalized = skill.Trim();
        // If it's a short acronym/name that doesn't look like a course title, append context
        if (normalized.Length <= 4 && !normalized.Contains(' '))
        {
            return normalized switch
            {
                _ when normalized.Equals("C#", StringComparison.OrdinalIgnoreCase) => "C# Programming",
                _ when normalized.Equals("SQL", StringComparison.OrdinalIgnoreCase) => "SQL & Databases",
                _ when normalized.Equals("CSS", StringComparison.OrdinalIgnoreCase) => "CSS & Styling",
                _ when normalized.Equals("HTML", StringComparison.OrdinalIgnoreCase) => "HTML & Web Fundamentals",
                _ when normalized.Equals("Git", StringComparison.OrdinalIgnoreCase) => "Git & Version Control",
                _ when normalized.Equals("AWS", StringComparison.OrdinalIgnoreCase) => "AWS Cloud Services",
                _ when normalized.Equals("GCP", StringComparison.OrdinalIgnoreCase) => "Google Cloud Platform",
                _ when normalized.Equals("CI", StringComparison.OrdinalIgnoreCase) => "CI/CD & DevOps",
                _ when normalized.Equals("REST", StringComparison.OrdinalIgnoreCase) => "REST API Design",
                _ => normalized
            };
        }
        return normalized;
    }

    private static bool HasSufficientRoadmapProfile(UserProfileEntity? userProfile)
    {
        if (userProfile is null)
            return false;

        var hasRole = !string.IsNullOrWhiteSpace(userProfile.TargetRole);
        var hasExperience = !string.IsNullOrWhiteSpace(userProfile.ExperienceLevel);
        var hasInterests = userProfile.Interests?.Count > 0;

        // Role + experience is the gold standard; role + interests is acceptable fallback.
        return (hasRole && hasExperience) || (hasRole && hasInterests);
    }

    private static bool LooksLikeRole(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return ContainsAny(normalized,
            "engineer", "developer", "architect", "analyst", "scientist",
            "designer", "manager", "lead", "devops", "qa", "tester",
            "مهندس", "مطور", "محلل", "مصمم", "مدير");
    }

    private static AskUserQuestion[] BuildRoadmapDiscoveryQuestions(UserProfileEntity? userProfile, string query, string conversationContext)
    {
        var combinedContext = $"{conversationContext}\n{query}".ToLowerInvariant();
        var isArabic = ContainsArabic(query) || ContainsArabic(conversationContext);
        var casualTone = LooksCasualTone(query);

        var hasExperience = !string.IsNullOrWhiteSpace(userProfile?.ExperienceLevel);
        var hasTargetRole = !string.IsNullOrWhiteSpace(userProfile?.TargetRole);

        var hasResourcePreference = (userProfile?.Constraints?.Any(c => !string.IsNullOrWhiteSpace(c)) ?? false)
            || ContainsAny(combinedContext, "free", "paid", "budget", "no paid", "free only", "مجاني", "مدفوع", "ميزانية");

        var questions = new List<AskUserQuestion>();

        if (!hasExperience)
        {
            questions.Add(new AskUserQuestion
            {
                Id = "roadmap_experience",
                Text = isArabic
                    ? (casualTone ? "مستواك الحالي إيه في المجال؟" : "ما مستوى خبرتك الحالي؟")
                    : (casualTone ? "What level are you at right now?" : "What is your current experience level?"),
                Type = "checkbox",
                Options = isArabic
                    ? ["مبتدئ", "متوسط", "متقدم"]
                    : ["Beginner", "Intermediate", "Advanced"],
                Required = true
            });
        }

        if (!hasTargetRole)
        {
            questions.Add(new AskUserQuestion
            {
                Id = "roadmap_target_role",
                Text = isArabic
                    ? (casualTone ? "حابب توصل لأنهي دور/وظيفة؟" : "ما الدور المستهدف من هذه الخطة؟")
                    : (casualTone ? "Which role are you aiming for with this roadmap?" : "Which role are you targeting with this roadmap?"),
                Type = "text",
                Placeholder = isArabic ? "مثال: Software Engineer" : "Example: Software Engineer",
                Required = true
            });
        }

        if (!hasResourcePreference)
        {
            questions.Add(new AskUserQuestion
            {
                Id = "roadmap_budget",
                Text = isArabic
                    ? (casualTone ? "تفضّل مصادر مجانية بس ولا عادي المدفوع؟" : "ما تفضيلك للمصادر التعليمية؟")
                    : (casualTone ? "Do you want free resources only, or is paid okay too?" : "Which learning resources do you prefer?"),
                Type = "checkbox",
                Options = isArabic
                    ? ["مجاني فقط", "المدفوع مناسب"]
                    : ["Free only", "Paid is okay"],
                Required = true
            });
        }

        if (questions.Count == 0)
        {
            questions.Add(new AskUserQuestion
            {
                Id = "roadmap_experience",
                Text = isArabic
                    ? (casualTone ? "مستواك الحالي إيه في المجال؟" : "ما مستوى خبرتك الحالي؟")
                    : (casualTone ? "What level are you at right now?" : "What is your current experience level?"),
                Type = "checkbox",
                Options = isArabic
                    ? ["مبتدئ", "متوسط", "متقدم"]
                    : ["Beginner", "Intermediate", "Advanced"],
                Required = true
            });
        }

        return [.. questions.Take(3)];
    }

    private static bool ContainsAny(string text, params string[] terms)
        => terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsArabic(string text)
        => !string.IsNullOrWhiteSpace(text) && text.Any(ch => ch >= '\u0600' && ch <= '\u06FF');

    private static bool LooksCasualTone(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.ToLowerInvariant();
        return normalized.Contains("wanna")
               || normalized.Contains("gonna")
               || normalized.Contains("pls")
               || normalized.Contains("bro")
               || normalized.Contains("ya ")
               || normalized.Contains("عايز")
               || normalized.Contains("محتاج")
               || normalized.Contains("ايه")
               || normalized.Contains("؟");
    }

    private static AskUserQuestion[] BuildRoadmapConfirmationQuestions()
    {
        return
        [
            new AskUserQuestion
            {
                Id = "roadmap_confirm",
                Text = "I detected a roadmap request. Do you want me to generate your roadmap now?",
                Type = "checkbox",
                Options = ["Yes, generate roadmap now", "Not now"],
                Required = true
            }
        ];
    }

    private static bool IsExplicitRoadmapRequest(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var normalized = query.Trim().ToLowerInvariant();

        var roadmapTerms = new[]
        {
            "roadmap",
            "learning path",
            "study plan",
            "learning plan",
            "plan for",
            "خريطة طريق",
            "مسار تعلم",
            "خطة تعلم",
            "خطة دراسة",
            "رودماب"
        };

        return roadmapTerms.Any(normalized.Contains);
    }

    private static bool IsRoadmapIntakeQuestionSet(AskUserQuestion[] questions)
    {
        if (questions.Length == 0)
            return false;

        static bool ContainsRoadmapIntakeTerms(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = text.Trim().ToLowerInvariant();
            var terms = new[]
            {
                "roadmap",
                "learning path",
                "target role",
                "job title",
                "experience level",
                "career goal",
                "generate your roadmap",
                "خريطة طريق",
                "مسار تعلم",
                "المسمى الوظيفي",
                "مستوى الخبرة"
            };

            return terms.Any(normalized.Contains);
        }

        return questions.Any(q =>
            ContainsRoadmapIntakeTerms(q.Text)
            || q.Options?.Any(ContainsRoadmapIntakeTerms) == true);
    }

    private async Task<bool> IsRoadmapRefinementSemanticAsync(string query, string conversationContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(conversationContext))
            return false;

        try
        {
            var classifierSystem = "You are a strict binary classifier. Return JSON only. No explanation.";
            var prompt = $"""
Task:
Decide if the latest user message is asking to regenerate, modify, constrain, or refine an already existing roadmap in this conversation.

Return JSON with fields: is_refinement (true or false), confidence (0-100)

Guidelines:
- true when the user is changing roadmap preferences (budget/free-only, language, sources, difficulty, timeline, role focus) for an existing roadmap.
- false when the user is asking a general question unrelated to updating/regenerating roadmap output.

Conversation context:
{conversationContext}

Latest user message:
{query}
""";

            var (Response, ModelName, ThinkingContent) = await customerHelper.SendRequestWithModel(
                prompt,
                options.CurrentValue.Llm.ChatModel,
                useThinking: false,
                isSuperAdmin: IsSuperAdmin(),
                systemPrompt: classifierSystem);

            var responseText = Response;

            if (string.IsNullOrWhiteSpace(responseText))
                return false;

            var start = responseText.IndexOf('{');
            var end = responseText.LastIndexOf('}');
            if (start < 0 || end <= start)
                return false;

            var json = responseText[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("is_refinement", out var refinementProp))
                return false;

            return refinementProp.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic roadmap refinement classification failed; falling back to default routing.");
            return false;
        }
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

    private static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "en";
        int arabicCount = 0;
        int letterCount = 0;
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                letterCount++;
                if (c >= '\u0600' && c <= '\u06FF') arabicCount++;
            }
        }
        return letterCount > 0 && (double)arabicCount / letterCount > 0.3 ? "ar" : "en";
    }

    private static HashSet<string> ExtractAskedQuestionIds(string conversationContext)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(conversationContext)) return ids;
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(conversationContext, @"\[ASKED_QUESTIONS:([^\]]+)\]"))
        {
            foreach (var id in m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                ids.Add(id);
        }
        return ids;
    }

    private static AskUserQuestion[] DeduplicateQuestions(AskUserQuestion[] questions, HashSet<string> askedIds)
    {
        if (questions is not { Length: > 0 } || askedIds.Count == 0) return questions;
        var filtered = questions.Where(q => !askedIds.Contains(q.Id)).ToArray();
        return filtered.Length > 0 ? filtered : questions; // fallback: if all deduped, keep originals to avoid empty
    }

    private static void MarkQuestionsAsked(HashSet<string> askedIds, AskUserQuestion[] questions)
    {
        foreach (var q in questions)
            askedIds.Add(q.Id);
    }

    private string FormatSse(string eventName, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return $"event: {eventName}\ndata: {json}";
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

    private static async Task<(string Mode, string? Error)> ClassifyModeWithFallbackAsync(
        CustomerHelper customerHelper,
        string query,
        int sessionMessageCount,
        bool profileComplete,
        bool applyFriendBias,
        ILogger logger)
    {
        try
        {
            var classifiedMode = await customerHelper.ClassifyModeAsync(query, sessionMessageCount, profileComplete);

            if (applyFriendBias && classifiedMode == "ACTION" && !profileComplete)
            {
                classifiedMode = "FRIEND";
                logger.LogDebug("Applied FRIEND bias for new session with incomplete profile. Original: ACTION, Query: {Query}", query);
            }

            return (classifiedMode, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Mode classification failed, defaulting to ACTION");
            return ("ACTION", ex.Message);
        }
    }

    private const string RoadmapDiscoveryAttemptPrefix = "roadmap_discovery_attempt:";

    private async Task SaveRoadmapFlowStateAsync(string userId, string sessionId, string state)
    {
        await conversationContextManager.AddMessageToContextAsync(
            userId,
            sessionId,
            RoadmapIntentHelper.BuildRoadmapStateMessage(state),
            "state");

        if (string.Equals(state, RoadmapIntentHelper.RoadmapStateDiscoveryAsked, StringComparison.OrdinalIgnoreCase))
        {
            var currentCount = await TryGetPersistedDiscoveryAttemptCountAsync(userId, sessionId) ?? 0;
            var nextCount = currentCount + 1;

            await conversationContextManager.AddMessageToContextAsync(
                userId,
                sessionId,
                BuildRoadmapDiscoveryAttemptMessage(nextCount),
                "state");
        }
        else if (string.Equals(state, RoadmapIntentHelper.RoadmapStateIdle, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(state, RoadmapIntentHelper.RoadmapStateCompleted, StringComparison.OrdinalIgnoreCase))
        {
            await conversationContextManager.AddMessageToContextAsync(
                userId,
                sessionId,
                BuildRoadmapDiscoveryAttemptMessage(0),
                "state");
        }
    }

    private static string BuildRoadmapDiscoveryAttemptMessage(int attemptCount)
    {
        var normalized = Math.Max(0, attemptCount);
        return $"{RoadmapDiscoveryAttemptPrefix}{normalized}";
    }

    private static int? ExtractRoadmapDiscoveryAttempt(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        if (!message.StartsWith(RoadmapDiscoveryAttemptPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var rawValue = message[RoadmapDiscoveryAttemptPrefix.Length..].Trim();
        return int.TryParse(rawValue, out var parsed) && parsed >= 0 ? parsed : null;
    }

    private async Task<int?> TryGetPersistedDiscoveryAttemptCountAsync(string userId, string sessionId)
    {
        var latestAttemptMessage = await chatRepository.GetLatestStateMessageByPrefixAsync(
            userId,
            sessionId,
            RoadmapDiscoveryAttemptPrefix);

        var latestAttempt = ExtractRoadmapDiscoveryAttempt(latestAttemptMessage);
        if (latestAttempt.HasValue)
            return latestAttempt.Value;

        var messages = await chatRepository.GetMessagesAsync(userId, sessionId, 200);
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var message = messages[i];
            if (!string.Equals(message.Role, "state", StringComparison.OrdinalIgnoreCase))
                continue;

            var parsed = ExtractRoadmapDiscoveryAttempt(message.Message);
            if (parsed.HasValue)
                return parsed.Value;
        }

        return null;
    }

    // Counts discovery_asked states only within the current cycle (since last idle/completed).
    private async Task<int> GetDiscoveryAttemptCountAsync(string userId, string sessionId)
    {
        var persistedCount = await TryGetPersistedDiscoveryAttemptCountAsync(userId, sessionId);
        if (persistedCount.HasValue)
            return persistedCount.Value;

        var messages = await chatRepository.GetMessagesAsync(userId, sessionId, 500);
        var messageList = messages.OrderBy(m => m.CreatedAt).ToList(); // ascending = chronological

        var lastCycleResetIndex = -1;
        for (var i = messageList.Count - 1; i >= 0; i--)
        {
            var m = messageList[i];
            if (m.Role == "state" && m.Message != null &&
                (m.Message.Contains(RoadmapIntentHelper.RoadmapStateIdle, StringComparison.OrdinalIgnoreCase)
                 || m.Message.Contains(RoadmapIntentHelper.RoadmapStateCompleted, StringComparison.OrdinalIgnoreCase)))
            {
                lastCycleResetIndex = i;
                break;
            }
        }

        return messageList
            .Skip(lastCycleResetIndex + 1)
            .Count(m =>
                m.Role == "state"
                && m.Message?.Contains(RoadmapIntentHelper.RoadmapStateDiscoveryAsked, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static List<string> GetMissingProfileFields(UserProfileEntity? userProfile)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(userProfile?.TargetRole))
            missing.Add("Target Role (what job/role are they aiming for?)");

        var hasInterests = userProfile?.Interests?.Count > 0;
        if (string.IsNullOrWhiteSpace(userProfile?.ExperienceLevel) && !hasInterests)
            missing.Add("Experience Level (beginner, intermediate, or advanced?)");

        return missing;
    }

    private async Task<string?> GetRoadmapFlowStateAsync(string userId, string sessionId)
    {
        var latestStateMessage = await chatRepository.GetLatestStateMessageByPrefixAsync(
            userId,
            sessionId,
            RoadmapIntentHelper.RoadmapStatePrefix);

        var latestState = RoadmapIntentHelper.ExtractRoadmapState(latestStateMessage);
        if (!string.IsNullOrWhiteSpace(latestState))
            return latestState;

        var messages = await chatRepository.GetMessagesAsync(userId, sessionId, 80);

        foreach (var message in messages.OrderByDescending(m => m.CreatedAt))
        {
            if (!string.Equals(message.Role, "state", StringComparison.OrdinalIgnoreCase))
                continue;

            var extractedState = RoadmapIntentHelper.ExtractRoadmapState(message.Message);
            if (!string.IsNullOrWhiteSpace(extractedState))
                return extractedState;
        }

        return null;
    }

    private async Task TryPersistProfileFromLatestMessageAsync(string userId, string query)
    {
        var extracted = ExtractProfileFromUserMessage(userId, query);
        if (extracted is null)
            return;

        try
        {
            await userProfileRepository.UpsertAsync(extracted);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist profile signals from latest message for user {UserId}", userId);
        }
    }

    private static UserProfileEntity? ExtractProfileFromConversationContext(string userId, string conversationContext)
    {
        if (string.IsNullOrWhiteSpace(conversationContext))
            return null;

        var userOnlyText = new StringBuilder();
        var inUserBlock = false;

        var lines = conversationContext
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l));

        foreach (var line in lines)
        {
            if (line.StartsWith("[user]:", StringComparison.OrdinalIgnoreCase))
            {
                inUserBlock = true;
                var payload = line[7..].Trim();
                if (!string.IsNullOrWhiteSpace(payload))
                    userOnlyText.AppendLine(payload);
                continue;
            }

            if (line.StartsWith("[assistant]:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("[summary", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("[state", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("[title", StringComparison.OrdinalIgnoreCase))
            {
                inUserBlock = false;
                continue;
            }

            if (inUserBlock)
                userOnlyText.AppendLine(line);
        }

        if (userOnlyText.Length == 0)
            return null;

        return ExtractProfileFromUserMessage(userId, userOnlyText.ToString());
    }

    private static UserProfileEntity? ExtractProfileFromUserMessage(string userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var profile = new UserProfileEntity
        {
            UserId = userId,
            Interests = [],
            Constraints = []
        };

        var lines = query
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        foreach (var line in lines)
        {
            // Supports "label: answer" and "Question?: answer" (AskUserBlock format).
            int separatorIdx;
            int answerStart;
            var qColonIdx = line.IndexOf("?: ", StringComparison.Ordinal);
            if (qColonIdx > 0)
            {
                separatorIdx = qColonIdx;
                answerStart = qColonIdx + 3;
            }
            else
            {
                separatorIdx = line.IndexOf(':');
                answerStart = separatorIdx + 1;
            }

            if (separatorIdx <= 0 || answerStart >= line.Length)
                continue;

            var label = line[..separatorIdx].Trim().ToLowerInvariant();
            var answer = line[answerStart..].Trim();
            if (string.IsNullOrWhiteSpace(answer))
                continue;

            if (ContainsAny(label, "experience", "level", "background", "skill level", "proficiency",
                "roadmap_experience", "how far along", "where are you",
                "مستواك", "خبر", "مستوى", "مستواك الحالي"))
            {
                profile.ExperienceLevel = answer;
                continue;
            }

            if (ContainsAny(label, "role", "job", "target", "career", "position", "aiming",
                "aspire", "goal", "what do you want to become", "what position",
                "roadmap_target_role",
                "وظيفة", "دور", "منصب", "حابب توصل", "تبغى تصير", "هدفك"))
            {
                profile.TargetRole = answer;
                continue;
            }

            if (ContainsAny(label, "resource", "budget", "free", "paid", "cost", "pricing",
                "roadmap_budget",
                "مصادر", "ميزانية", "مجاني", "مدفوع", "تفضّل مصادر", "تكلفة"))
            {
                profile.Constraints!.Add(answer);
                continue;
            }

            if (ContainsAny(label, "focus", "topic", "interest", "prioritize", "speciali",
                "roadmap_focus", "area", "domain", "field",
                "تركيز", "موضوع", "اهتم", "مجال", "تخصص",
                "الجوانب", "تريد التركيز", "which topics", "prioritize first"))
            {
                var interests = answer
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                profile.Interests!.AddRange(interests);

                // Only promote single-value focus answers to TargetRole if they look like a role.
                if (string.IsNullOrWhiteSpace(profile.TargetRole)
                    && interests.Count == 1
                    && LooksLikeRole(interests[0]))
                {
                    profile.TargetRole = interests[0];
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.ExperienceLevel)
            || !string.IsNullOrWhiteSpace(profile.TargetRole)
            || profile.Constraints!.Count > 0
            || profile.Interests!.Count > 0)
        {
            profile.Constraints = [.. profile.Constraints!.Distinct(StringComparer.OrdinalIgnoreCase)];
            profile.Interests = [.. profile.Interests!.Distinct(StringComparer.OrdinalIgnoreCase)];
            return profile;
        }

        var trimmedLower = query.Trim().ToLowerInvariant();
        var bareSignalFound = false;

        if (ContainsAny(trimmedLower, "beginner", "مبتدئ"))
        {
            profile.ExperienceLevel = "Beginner";
            bareSignalFound = true;
        }
        else if (ContainsAny(trimmedLower, "intermediate", "متوسط"))
        {
            profile.ExperienceLevel = "Intermediate";
            bareSignalFound = true;
        }
        else if (ContainsAny(trimmedLower, "advanced", "متقدم"))
        {
            profile.ExperienceLevel = "Advanced";
            bareSignalFound = true;
        }

        if (ContainsAny(trimmedLower, "paid is okay", "المدفوع مناسب"))
        {
            profile.Constraints!.Add("Paid is okay");
            bareSignalFound = true;
        }
        else if (ContainsAny(trimmedLower, "free only", "no paid", "مجاني فقط", "بدون مدفوع"))
        {
            profile.Constraints!.Add("Free only");
            bareSignalFound = true;
        }

        return bareSignalFound ? profile : null;
    }
}
