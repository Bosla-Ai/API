using System.Net;
using Domain.Exceptions;
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
    private readonly SemaphoreSlim _containerInitLock = new(1, 1);
    private Task<Container>? _containerTask;

    private async Task<Container> GetContainerAsync()
    {
        var existingTask = _containerTask;
        if (existingTask != null && !existingTask.IsFaulted && !existingTask.IsCanceled)
        {
            return await existingTask;
        }

        await _containerInitLock.WaitAsync();
        try
        {
            existingTask = _containerTask;
            if (existingTask == null || existingTask.IsFaulted || existingTask.IsCanceled)
            {
                _containerTask = InitializeContainerWithRetryAsync();
                existingTask = _containerTask;
            }

            return await existingTask;
        }
        finally
        {
            _containerInitLock.Release();
        }
    }

    private async Task<Container> InitializeContainerWithRetryAsync()
    {
        var delays = new[]
        {
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500)
        };

        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
                var database = databaseResponse.Database;
                var containerResponse = await database.CreateContainerIfNotExistsAsync(_containerName, "/UserId");
                return containerResponse.Container;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                await Task.Delay(delays[attempt - 1]);
            }
        }

        throw new InternalServerErrorException("Unable to access chat storage at the moment.");
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is TaskCanceledException or HttpRequestException)
        {
            return true;
        }

        if (ex is CosmosException cosmosEx)
        {
            return cosmosEx.StatusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;
        }

        return false;
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

