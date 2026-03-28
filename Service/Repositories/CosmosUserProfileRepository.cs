using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class CosmosUserProfileRepository(
    CosmosClient cosmosClient,
    IConfiguration configuration,
    ILogger<CosmosUserProfileRepository> logger) : IUserProfileRepository
{
    private readonly CosmosClient _cosmosClient = cosmosClient;
    private readonly string _databaseName = configuration["CosmosDb:DatabaseName"] ?? "BoslaChat";
    private readonly string _containerName = configuration["CosmosDb:ProfileContainerName"] ?? "user_profiles";
    private readonly ILogger<CosmosUserProfileRepository> _logger = logger;
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

            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
            var database = databaseResponse.Database;

            // Partition by userId for efficient single-user queries
            var containerResponse = await database.CreateContainerIfNotExistsAsync(_containerName, "/UserId");
            _container = containerResponse.Container;

            _logger.LogInformation("Initialized Cosmos container '{ContainerName}' for user profiles", _containerName);
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<UserProfileEntity?> GetByUserIdAsync(string userId)
    {
        try
        {
            var container = await GetContainerAsync();

            // Query by userId (we store one document per user, id == userId)
            var response = await container.ReadItemAsync<UserProfileEntity>(
                userId,
                new PartitionKey(userId));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        // Let other exceptions propagate - don't mask network/auth/throttling errors
    }

    public async Task UpsertAsync(UserProfileEntity profile)
    {
        var container = await GetContainerAsync();

        // Ensure id matches userId for consistent retrieval
        profile.Id = profile.UserId;
        profile.UpdatedAt = DateTime.UtcNow;

        // Try to get existing profile for smart merge
        UserProfileEntity? existing = null;
        try
        {
            var response = await container.ReadItemAsync<UserProfileEntity>(
                profile.UserId,
                new PartitionKey(profile.UserId));
            existing = response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Profile doesn't exist yet, will create new
        }
        // Let other exceptions propagate

        if (existing != null)
        {
            // Smart merge: combine arrays, prefer newer non-null scalars
            existing.MergeFrom(profile);
            await container.UpsertItemAsync(existing, new PartitionKey(profile.UserId));
            _logger.LogDebug("Merged and updated profile for user {UserId}, extraction count: {Count}",
                profile.UserId, existing.ExtractionCount);
        }
        else
        {
            // First profile for this user
            profile.CreatedAt = DateTime.UtcNow;
            profile.ExtractionCount = 1;
            await container.CreateItemAsync(profile, new PartitionKey(profile.UserId));
            _logger.LogDebug("Created new profile for user {UserId}", profile.UserId);
        }
    }

    public async Task DeleteAsync(string userId)
    {
        try
        {
            var container = await GetContainerAsync();
            await container.DeleteItemAsync<UserProfileEntity>(userId, new PartitionKey(userId));
            _logger.LogInformation("Deleted profile for user {UserId}", userId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted, ignore
            _logger.LogDebug("Profile for user {UserId} not found during delete", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string userId)
    {
        var profile = await GetByUserIdAsync(userId);
        return profile != null;
    }
}
