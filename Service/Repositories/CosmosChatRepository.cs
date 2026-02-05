using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class CosmosChatRepository : IChatRepository
{
    private readonly Container _container;

    public CosmosChatRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "BoslaChat";
        var containerName = configuration["CosmosDb:ContainerName"] ?? "chat_messages";

        var databaseResponse = cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult();
        var database = databaseResponse.Database;

        var containerResponse = database.CreateContainerIfNotExistsAsync(containerName, "/UserId").GetAwaiter().GetResult();
        _container = containerResponse.Container;
    }

    public async Task AddMessageAsync(ChatMessageEntity message)
    {
        await _container.CreateItemAsync(message, new PartitionKey(message.UserId));
    }

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(string userId, string sessionId, int limit = 15)
    {
        var query = _container.GetItemLinqQueryable<ChatMessageEntity>(
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
        var partitionKey = new PartitionKey(userId);

        foreach (var id in messageIds)
        {
            try
            {
                await _container.DeleteItemAsync<ChatMessageEntity>(id, partitionKey);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Message already deleted, ignore
            }
        }
    }
}
