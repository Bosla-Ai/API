using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Service.Helpers;
using Shared.DTOs;
using Shared.Enums;
using Shared.Options;

namespace BoslaAPI.Tests.Services;

public class FastModeContextTests
{
    [Fact]
    public async Task FastModeFallback_ReusesSamePromptAcrossQwenToGroqSwitch()
    {
        var handler = new CapturingHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "cerebras.test")
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":\"rate limit\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"ok from fallback\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var helper = CreateCustomerHelper(
            BuildAiOptions(
                summarizationThreshold: 10,
                contextCompactionCharThreshold: 10_000,
                llmApiUrl: "https://cerebras.test/v1/chat/completions",
                llmModel: "qwen-fast",
                chatModel: "qwen-fast",
                groqApiUrl: "https://groq.test/openai/v1/chat/completions",
                groqModel: "openai/gpt-oss-120b"),
            handler);

        const string prompt = "Context:\nRecent Conversation:\n[user]: shared session facts\n\nCurrent User Query: continue";
        const string systemPrompt = "system prompt";

        var (Response, ModelName, ThinkingContent) = await helper.SendRequestByTask(
            prompt,
            LLMInteractionType.ChatWithAI,
            useThinking: false,
            chatMode: ChatMode.Fast,
            systemPrompt: systemPrompt);

        ModelName.Should().Be("openai/gpt-oss-120b");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Body.Should().Contain("\"model\":\"qwen-fast\"");
        handler.Requests[1].Body.Should().Contain("\"model\":\"openai/gpt-oss-120b\"");
        handler.Requests[0].Body.Should().Contain("shared session facts");
        handler.Requests[1].Body.Should().Contain("shared session facts");
        handler.Requests[0].Body.Should().Contain("system prompt");
        handler.Requests[1].Body.Should().Contain("system prompt");
    }

    [Fact]
    public async Task ConversationContext_IncludesLatestSummaryEvenWhenRecentWindowDropsIt()
    {
        var repository = new InMemoryChatRepository();
        var baseTime = DateTime.UtcNow.AddMinutes(-30);

        await repository.AddMessageAsync(new ChatMessageEntity
        {
            Id = "summary-1",
            UserId = "u1",
            SessionId = "s1",
            Role = "summary",
            Message = "User wants backend engineering and free resources only.",
            CreatedAt = baseTime
        });

        for (var i = 0; i < 20; i++)
        {
            await repository.AddMessageAsync(new ChatMessageEntity
            {
                Id = $"msg-{i}",
                UserId = "u1",
                SessionId = "s1",
                Role = i % 2 == 0 ? "user" : "assistant",
                Message = $"recent message {i}",
                CreatedAt = baseTime.AddMinutes(i + 1)
            });
        }

        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"unused\"}}]}",
                Encoding.UTF8,
                "application/json")
        });

        var sut = CreateConversationContextManager(
            repository,
            BuildAiOptions(summarizationThreshold: 10, contextCompactionCharThreshold: 10_000),
            handler);

        var context = await sut.GetConversationContextAsync("u1", "s1");

        context.Should().Contain("[Previous Compacted Context]: User wants backend engineering and free resources only.");
        context.Should().Contain("[assistant]: recent message 19");
        handler.Requests.Should().BeEmpty("the existing summary should prevent unnecessary recompaction");
    }

    [Fact]
    public async Task ConversationContext_RecompactionCarriesForwardExistingSummary()
    {
        var repository = new InMemoryChatRepository();
        var baseTime = DateTime.UtcNow.AddMinutes(-10);

        await repository.AddMessageAsync(new ChatMessageEntity
        {
            Id = "summary-old",
            UserId = "u1",
            SessionId = "s1",
            Role = "summary",
            Message = "Existing summary of earlier turns and decisions.",
            CreatedAt = baseTime
        });

        for (var i = 0; i < 4; i++)
        {
            await repository.AddMessageAsync(new ChatMessageEntity
            {
                Id = $"recent-{i}",
                UserId = "u1",
                SessionId = "s1",
                Role = i % 2 == 0 ? "user" : "assistant",
                Message = $"recent message {i} " + new string((char)('a' + i), 2_500),
                CreatedAt = baseTime.AddMinutes(i + 1)
            });
        }

        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"Merged summary\"}}]}",
                Encoding.UTF8,
                "application/json")
        });

        var sut = CreateConversationContextManager(
            repository,
            BuildAiOptions(summarizationThreshold: 10, contextCompactionCharThreshold: 8_000),
            handler);

        var context = await sut.GetConversationContextAsync("u1", "s1");
        var latestSummary = await repository.GetLatestSummaryMessageAsync("u1", "s1");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Body.Should().Contain("Existing summary of earlier turns and decisions.");
        handler.Requests[0].Body.Should().Contain("recent message 0");
        latestSummary.Should().NotBeNull();
        latestSummary!.Message.Should().Be("Merged summary");
        context.Should().Contain("[Previous Compacted Context]: Merged summary");
    }

    private static ConversationContextManager CreateConversationContextManager(
        IChatRepository repository,
        AiOptions options,
        HttpMessageHandler handler)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var helper = CreateCustomerHelper(options, handler);
        return new ConversationContextManager(cache, repository, helper, new StaticOptionsMonitor<AiOptions>(options));
    }

    private static CustomerHelper CreateCustomerHelper(AiOptions options, HttpMessageHandler handler)
    {
        var logger = LoggerFactory.Create(builder => { }).CreateLogger<CustomerHelper>();
        var monitor = new StaticOptionsMonitor<AiOptions>(options);
        var rateLimiter = new UserRateLimiter(monitor);
        return new CustomerHelper(logger, new HttpClient(handler), monitor, rateLimiter);
    }

    private static AiOptions BuildAiOptions(
        int summarizationThreshold,
        int contextCompactionCharThreshold,
        string llmApiUrl = "https://cerebras.test/v1/chat/completions",
        string llmModel = "qwen-3-235b-a22b-instruct-2507",
        string chatModel = "qwen-3-235b-a22b-instruct-2507",
        string groqApiUrl = "https://groq.test/openai/v1/chat/completions",
        string groqModel = "openai/gpt-oss-120b")
    {
        return new AiOptions
        {
            Llm = new LlmOptions
            {
                Provider = "cerebras",
                ApiKeys = ["llm-key"],
                ApiUrl = llmApiUrl,
                Model = llmModel,
                ChatModel = chatModel,
                ReasoningModel = llmModel,
                SummarizationThreshold = summarizationThreshold,
                ContextCompactionCharThreshold = contextCompactionCharThreshold,
                ContextMaxMessageLength = 8_000
            },
            Groq = new GroqOptions
            {
                ApiKeys = ["groq-key"],
                ApiUrl = groqApiUrl,
                Model = groqModel
            },
            Gemini = new GeminiOptions(),
            Mistral = new MistralOptions(),
            Prompts = new PromptOptions()
        };
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class InMemoryChatRepository : IChatRepository
    {
        private readonly List<ChatMessageEntity> _messages = [];

        public Task AddMessageAsync(ChatMessageEntity message)
        {
            _messages.Add(Clone(message));
            return Task.CompletedTask;
        }

        public Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 15)
        {
            var messages = _messages
                .Where(m => m.UserId == userId && m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .TakeLast(limit)
                .Select(Clone)
                .ToList();

            return Task.FromResult(messages);
        }

        public Task<ChatMessageEntity?> GetLatestSummaryMessageAsync(string userId, string sessionId)
        {
            var summary = _messages
                .Where(m => m.UserId == userId && m.SessionId == sessionId && m.Role == "summary")
                .OrderByDescending(m => m.CreatedAt)
                .Select(Clone)
                .FirstOrDefault();

            return Task.FromResult(summary);
        }

        public Task<string?> GetLatestStateMessageByPrefixAsync(string userId, string sessionId, string prefix)
        {
            var state = _messages
                .Where(m => m.UserId == userId
                            && m.SessionId == sessionId
                            && m.Role == "state"
                            && m.Message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Message)
                .FirstOrDefault();

            return Task.FromResult(state);
        }

        public Task DeleteMessagesAsync(string userId, string sessionId, IEnumerable<string> messageIds)
        {
            var ids = messageIds.ToHashSet(StringComparer.Ordinal);
            _messages.RemoveAll(m => m.UserId == userId && m.SessionId == sessionId && ids.Contains(m.Id));
            return Task.CompletedTask;
        }

        public Task<List<ChatMessageEntity>> GetAllUserMessagesAsync(string userId)
        {
            return Task.FromResult(_messages.Where(m => m.UserId == userId).Select(Clone).ToList());
        }

        public Task TouchSessionAsync(string userId, string sessionId) => Task.CompletedTask;

        public Task<int> DeleteSessionAsync(string userId, string sessionId)
        {
            var before = _messages.Count;
            _messages.RemoveAll(m => m.UserId == userId && m.SessionId == sessionId);
            return Task.FromResult(before - _messages.Count);
        }

        public Task<int> DeleteInactiveMessagesAsync(DateTime createdBefore, DateTime accessedBefore)
        {
            var before = _messages.Count;
            _messages.RemoveAll(m => m.CreatedAt < createdBefore && m.LastAccessedAt < accessedBefore);
            return Task.FromResult(before - _messages.Count);
        }

        private static ChatMessageEntity Clone(ChatMessageEntity message)
        {
            return new ChatMessageEntity
            {
                Id = message.Id,
                UserId = message.UserId,
                SessionId = message.SessionId,
                Message = message.Message,
                Role = message.Role,
                CreatedAt = message.CreatedAt,
                LastAccessedAt = message.LastAccessedAt
            };
        }
    }

    private sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add((request.RequestUri?.ToString() ?? string.Empty, body));
            return responder(request);
        }
    }
}
