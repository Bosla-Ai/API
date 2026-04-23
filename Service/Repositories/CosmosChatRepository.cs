using System.Collections.Concurrent;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class CosmosChatRepository(
    CosmosClient cosmosClient,
    IConfiguration configuration,
    ILogger<CosmosChatRepository> logger) : IChatRepository
{
    private readonly CosmosClient _cosmosClient = cosmosClient;
    private readonly ILogger<CosmosChatRepository> _logger = logger;
    private readonly string _databaseName = configuration["CosmosDb:DatabaseName"] ?? "BoslaChat";
    private readonly string _containerName = configuration["CosmosDb:ContainerName"] ?? "chat_messages";
    private readonly bool _autoProvisionResources = configuration.GetValue<bool?>("CosmosDb:AutoProvisionResources") ?? false;
    private static readonly ConcurrentDictionary<string, ChatMessageEntity> FallbackMessages = new(StringComparer.Ordinal);
    private Container? _container;
    private bool _fallbackOnly;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private async Task<Container?> GetContainerAsync()
    {
        if (_fallbackOnly)
            return null;

        if (_container != null)
            return _container;

        await _initLock.WaitAsync();
        try
        {
            if (_container != null)
                return _container;

            try
            {
                Database database;
                if (_autoProvisionResources)
                {
                    var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
                    database = databaseResponse.Database;
                    var containerResponse = await database.CreateContainerIfNotExistsAsync(_containerName, "/UserId");
                    _container = containerResponse.Container;
                }
                else
                {
                    database = _cosmosClient.GetDatabase(_databaseName);
                    await database.ReadAsync();
                    var existingContainer = database.GetContainer(_containerName);
                    await existingContainer.ReadContainerAsync();
                    _container = existingContainer;
                }

                return _container;
            }
            catch (Exception ex)
            {
                _fallbackOnly = true;
                _logger.LogWarning(ex,
                    _autoProvisionResources
                        ? "Failed to initialize Cosmos chat container '{ContainerName}'. Falling back to in-memory session storage."
                        : "Cosmos chat container '{ContainerName}' is unavailable and auto-provisioning is disabled. Falling back to in-memory session storage.",
                    _containerName);
                return null;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddMessageAsync(ChatMessageEntity message)
    {
        StoreFallbackMessage(message);

        var container = await GetContainerAsync();
        if (container == null)
            return;

        try
        {
            await container.CreateItemAsync(message, new PartitionKey(message.UserId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist chat message for {UserId}/{SessionId}; keeping in-memory fallback",
                message.UserId,
                message.SessionId);
        }
    }

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 50)
    {
        var combined = new Dictionary<string, ChatMessageEntity>(StringComparer.Ordinal);
        var container = await GetContainerAsync();

        if (container != null)
        {
            try
            {
                var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                        requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
                    .Where(m => m.SessionId == sessionId)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(limit);

                using var iterator = query.ToFeedIterator();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var message in response)
                        combined[message.Id] = CloneMessage(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch Cosmos chat messages for {UserId}/{SessionId}; using in-memory fallback",
                    userId,
                    sessionId);
            }
        }

        foreach (var message in GetFallbackMessages(userId, sessionId))
            combined[message.Id] = CloneMessage(message);

        return [.. combined.Values
            .OrderBy(m => m.CreatedAt)
            .TakeLast(limit)];
    }

    public async Task<ChatMessageEntity?> GetLatestSummaryMessageAsync(string userId, string sessionId)
    {
        ChatMessageEntity? latestSummary = null;
        var container = await GetContainerAsync();

        if (container != null)
        {
            try
            {
                var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                        requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
                    .Where(m => m.SessionId == sessionId && m.Role == "summary")
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(1);

                using var iterator = query.ToFeedIterator();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    latestSummary = response.FirstOrDefault();
                    if (latestSummary != null)
                    {
                        latestSummary = CloneMessage(latestSummary);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch latest Cosmos summary message for {UserId}/{SessionId}; using in-memory fallback",
                    userId,
                    sessionId);
            }
        }

        var fallbackSummary = GetFallbackMessages(userId, sessionId)
            .Where(m => string.Equals(m.Role, "summary", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        if (fallbackSummary != null && (latestSummary == null || fallbackSummary.CreatedAt > latestSummary.CreatedAt))
            return CloneMessage(fallbackSummary);

        return latestSummary;
    }

    public async Task<string?> GetLatestStateMessageByPrefixAsync(string userId, string sessionId, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        var messages = await GetMessagesAsync(userId, sessionId, 200);
        return messages
            .Where(m => string.Equals(m.Role, "state", StringComparison.OrdinalIgnoreCase)
                        && m.Message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.Message)
            .FirstOrDefault();
    }

    public async Task DeleteMessagesAsync(string userId, string sessionId, IEnumerable<string> messageIds)
    {
        var ids = messageIds as string[] ?? [.. messageIds];
        foreach (var id in ids)
            FallbackMessages.TryRemove(BuildFallbackMessageKey(userId, sessionId, id), out _);

        var container = await GetContainerAsync();
        if (container == null)
            return;

        var partitionKey = new PartitionKey(userId);

        foreach (var id in ids)
        {
            try
            {
                await container.DeleteItemAsync<ChatMessageEntity>(id, partitionKey);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Message already deleted, ignore.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete Cosmos chat message {MessageId} for {UserId}/{SessionId}",
                    id,
                    userId,
                    sessionId);
            }
        }
    }

    public async Task<List<ChatMessageEntity>> GetAllUserMessagesAsync(string userId)
    {
        var combined = new Dictionary<string, ChatMessageEntity>(StringComparer.Ordinal);
        var container = await GetContainerAsync();

        if (container != null)
        {
            try
            {
                var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                        requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
                    .Where(m => m.Role != "summary")
                    .OrderByDescending(m => m.CreatedAt);

                using var iterator = query.ToFeedIterator();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var message in response)
                        combined[message.Id] = CloneMessage(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch all Cosmos chat messages for {UserId}; using in-memory fallback",
                    userId);
            }
        }

        foreach (var message in FallbackMessages.Values.Where(m => m.UserId == userId))
            combined[message.Id] = CloneMessage(message);

        return [.. combined.Values.OrderByDescending(m => m.CreatedAt)];
    }

    public async Task TouchSessionAsync(string userId, string sessionId)
    {
        var now = DateTime.UtcNow;
        foreach (var message in GetFallbackMessages(userId, sessionId))
        {
            var updated = CloneMessage(message);
            updated.LastAccessedAt = now;
            FallbackMessages[BuildFallbackMessageKey(updated)] = updated;
        }

        var container = await GetContainerAsync();
        if (container == null)
            return;

        var messages = await GetMessagesAsync(userId, sessionId, limit: 200);
        foreach (var message in messages)
        {
            try
            {
                message.LastAccessedAt = now;
                await container.UpsertItemAsync(message, new PartitionKey(userId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to touch Cosmos chat message {MessageId} for {UserId}/{SessionId}",
                    message.Id,
                    userId,
                    sessionId);
            }
        }
    }

    public async Task<int> DeleteSessionAsync(string userId, string sessionId)
    {
        var messages = await GetMessagesAsync(userId, sessionId, limit: 500);
        if (messages.Count == 0)
            return 0;

        await DeleteMessagesAsync(userId, sessionId, messages.Select(m => m.Id));
        return messages.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count();
    }

    public async Task<int> DeleteInactiveMessagesAsync(DateTime createdBefore, DateTime accessedBefore)
    {
        var fallbackToDelete = FallbackMessages.Values
            .Where(m => m.CreatedAt < createdBefore && m.LastAccessedAt < accessedBefore)
            .ToList();

        foreach (var message in fallbackToDelete)
            FallbackMessages.TryRemove(BuildFallbackMessageKey(message), out _);

        var deletedCount = fallbackToDelete.Count;
        var container = await GetContainerAsync();
        if (container == null)
            return deletedCount;

        try
        {
            var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                    allowSynchronousQueryExecution: false,
                    requestOptions: new QueryRequestOptions { MaxConcurrency = -1 })
                .Where(m => m.CreatedAt < createdBefore && m.LastAccessedAt < accessedBefore);

            using var iterator = query.ToFeedIterator();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var message in response)
                {
                    try
                    {
                        await container.DeleteItemAsync<ChatMessageEntity>(
                            message.Id,
                            new PartitionKey(message.UserId));
                        deletedCount++;
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Already deleted.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete inactive Cosmos chat messages; returning fallback-only count");
        }

        return deletedCount;
    }

    private static string BuildFallbackMessageKey(ChatMessageEntity message)
        => BuildFallbackMessageKey(message.UserId, message.SessionId, message.Id);

    private static string BuildFallbackMessageKey(string userId, string sessionId, string messageId)
        => $"{userId}::{sessionId}::{messageId}";

    private static ChatMessageEntity CloneMessage(ChatMessageEntity message)
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

    private static void StoreFallbackMessage(ChatMessageEntity message)
    {
        FallbackMessages[BuildFallbackMessageKey(message)] = CloneMessage(message);
    }

    private static IEnumerable<ChatMessageEntity> GetFallbackMessages(string userId, string sessionId)
    {
        return FallbackMessages.Values
            .Where(m => m.UserId == userId && m.SessionId == sessionId)
            .Select(CloneMessage);
    }
}
