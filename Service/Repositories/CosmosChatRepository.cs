using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class CosmosChatRepository(CosmosClient cosmosClient, IConfiguration configuration) : IChatRepository
{
    private readonly CosmosClient _cosmosClient = cosmosClient;
    private readonly string _databaseName = configuration["CosmosDb:DatabaseName"] ?? "BoslaChat";
    private readonly string _containerName = configuration["CosmosDb:ContainerName"] ?? "chat_messages";
    private Container? _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private async Task<Container> GetContainerAsync()
    {
        if (_container != null)
            return _container;

        await _initLock.WaitAsync();
        try
        {
            if (_container != null)
                return _container;

            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName, throughput: 1000);
            var database = databaseResponse.Database;
            var containerResponse = await database.CreateContainerIfNotExistsAsync(_containerName, "/UserId");
            _container = containerResponse.Container;
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddMessageAsync(ChatMessageEntity message)
    {
        var container = await GetContainerAsync();
        await container.CreateItemAsync(message, new PartitionKey(message.UserId));
    }

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 50)
    {
        var container = await GetContainerAsync();
        var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit);

        var messages = new List<ChatMessageEntity>();

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            messages.AddRange(response);
        }

        messages.Reverse();
        return messages;
    }

    public async Task<string?> GetLatestStateMessageByPrefixAsync(string userId, string sessionId, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        var container = await GetContainerAsync();
        var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
            .Where(m => m.SessionId == sessionId
                        && m.Role == "state"
                        && m.Message != null
                        && m.Message.StartsWith(prefix))
            .OrderByDescending(m => m.CreatedAt)
            .Take(1);

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            var latest = response.FirstOrDefault();
            if (latest != null)
                return latest.Message;
        }

        return null;
    }

    public async Task DeleteMessagesAsync(string userId, string sessionId, IEnumerable<string> messageIds)
    {
        var container = await GetContainerAsync();
        var partitionKey = new PartitionKey(userId);

        foreach (var id in messageIds)
        {
            try
            {
                await container.DeleteItemAsync<ChatMessageEntity>(id, partitionKey);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Message already deleted, ignore
            }
        }
    }

    public async Task<List<ChatMessageEntity>> GetAllUserMessagesAsync(string userId)
    {
        var container = await GetContainerAsync();
        var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
            .Where(m => m.Role != "summary")
            .OrderByDescending(m => m.CreatedAt);

        var messages = new List<ChatMessageEntity>();

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            messages.AddRange(response);
        }

        return messages;
    }

    public async Task TouchSessionAsync(string userId, string sessionId)
    {
        var container = await GetContainerAsync();
        var messages = await GetMessagesAsync(userId, sessionId, limit: 200);
        var now = DateTime.UtcNow;

        foreach (var message in messages)
        {
            message.LastAccessedAt = now;
            await container.UpsertItemAsync(message, new PartitionKey(userId));
        }
    }

    public async Task<int> DeleteSessionAsync(string userId, string sessionId)
    {
        var container = await GetContainerAsync();
        var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) })
            .Where(m => m.SessionId == sessionId);

        var partitionKey = new PartitionKey(userId);
        var deletedCount = 0;

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var message in response)
            {
                try
                {
                    await container.DeleteItemAsync<ChatMessageEntity>(message.Id, partitionKey);
                    deletedCount++;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Already deleted, skip
                }
            }
        }

        return deletedCount;
    }

    public async Task<int> DeleteInactiveMessagesAsync(DateTime createdBefore, DateTime accessedBefore)
    {
        var container = await GetContainerAsync();
        // Dual condition: old enough AND not recently accessed
        var query = container.GetItemLinqQueryable<ChatMessageEntity>(
                allowSynchronousQueryExecution: false,
                requestOptions: new QueryRequestOptions { MaxConcurrency = -1 })
            .Where(m => m.CreatedAt < createdBefore && m.LastAccessedAt < accessedBefore);

        var deletedCount = 0;

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var message in response)
            {
                try
                {
                    await container.DeleteItemAsync<ChatMessageEntity>(
                        message.Id, new PartitionKey(message.UserId));
                    deletedCount++;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Already deleted, skip
                }
            }
        }

        return deletedCount;
    }
}

