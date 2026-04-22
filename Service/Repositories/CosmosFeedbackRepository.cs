using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class CosmosFeedbackRepository(
    CosmosClient cosmosClient,
    IConfiguration configuration,
    ILogger<CosmosFeedbackRepository> logger) : IFeedbackRepository
{
    private readonly string _databaseName = configuration["CosmosDb:DatabaseName"] ?? "BoslaChat";
    private readonly string _containerName = configuration["CosmosDb:FeedbackContainerName"] ?? "feedback";
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

            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName, throughput: 1000);
            var database = databaseResponse.Database;
            var containerResponse = await database.CreateContainerIfNotExistsAsync(_containerName, "/UserId");
            _container = containerResponse.Container;

            logger.LogInformation("Initialized Cosmos container '{ContainerName}' for feedback", _containerName);
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SubmitAsync(FeedbackEntity feedback)
    {
        var container = await GetContainerAsync();
        await container.CreateItemAsync(feedback, new PartitionKey(feedback.UserId));
        logger.LogDebug("Feedback submitted: {Rating} for session {SessionId}", feedback.Rating, feedback.SessionId);
    }

    public async Task<IReadOnlyList<FeedbackEntity>> GetBySessionAsync(string userId, string sessionId)
    {
        var container = await GetContainerAsync();
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.UserId = @userId AND c.SessionId = @sessionId ORDER BY c.CreatedAt DESC")
            .WithParameter("@userId", userId)
            .WithParameter("@sessionId", sessionId);

        var results = new List<FeedbackEntity>();
        using var iterator = container.GetItemQueryIterator<FeedbackEntity>(
            query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyList<FeedbackEntity>> GetAllAsync(int? limit = null)
    {
        Container container;
        try
        {
            container = await GetContainerAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize feedback container, returning empty list");
            return Array.Empty<FeedbackEntity>();
        }

        var sql = limit.HasValue
            ? $"SELECT TOP {limit.Value} * FROM c ORDER BY c.CreatedAt DESC"
            : "SELECT * FROM c ORDER BY c.CreatedAt DESC";

        var query = new QueryDefinition(sql);
        var results = new List<FeedbackEntity>();
        using var iterator = container.GetItemQueryIterator<FeedbackEntity>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }
}
