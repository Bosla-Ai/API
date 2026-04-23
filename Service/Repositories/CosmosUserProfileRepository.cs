using System.Collections.Concurrent;
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
    private readonly bool _autoProvisionResources = configuration.GetValue<bool?>("CosmosDb:AutoProvisionResources") ?? false;
    private readonly ILogger<CosmosUserProfileRepository> _logger = logger;
    private static readonly ConcurrentDictionary<string, UserProfileEntity> FallbackProfiles = new(StringComparer.Ordinal);
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

                var containerProperties = await _container.ReadContainerAsync();
                var partitionKeyPath = containerProperties.Resource.PartitionKeyPath;
                if (!string.Equals(partitionKeyPath, "/UserId", StringComparison.Ordinal))
                {
                    _fallbackOnly = true;
                    _logger.LogWarning(
                        "Cosmos container '{ContainerName}' uses partition key '{PartitionKey}'. Falling back to in-memory profile storage.",
                        _containerName,
                        partitionKeyPath);
                    _container = null;
                    return null;
                }

                _logger.LogInformation("Initialized Cosmos container '{ContainerName}' for user profiles", _containerName);
                return _container;
            }
            catch (Exception ex)
            {
                _fallbackOnly = true;
                _logger.LogWarning(ex,
                    _autoProvisionResources
                        ? "Failed to initialize Cosmos profile container '{ContainerName}'. Falling back to in-memory session storage."
                        : "Cosmos profile container '{ContainerName}' is unavailable and auto-provisioning is disabled. Falling back to in-memory session storage.",
                    _containerName);
                return null;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<UserProfileEntity?> GetByUserIdAsync(string userId)
    {
        var container = await GetContainerAsync();
        if (container != null)
        {
            try
            {
                var response = await container.ReadItemAsync<UserProfileEntity>(
                    userId,
                    new PartitionKey(userId));

                var resource = response.Resource;
                if (resource != null)
                {
                    FallbackProfiles[userId] = CloneProfile(resource);
                    return CloneProfile(resource);
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No persisted profile yet; fall through to fallback cache.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to read Cosmos user profile for {UserId}; using in-memory fallback",
                    userId);
            }
        }

        return FallbackProfiles.TryGetValue(userId, out var cachedProfile)
            ? CloneProfile(cachedProfile)
            : null;
    }

    public async Task UpsertAsync(UserProfileEntity profile)
    {
        profile.Id = profile.UserId;
        profile.UpdatedAt = DateTime.UtcNow;

        var existing = await GetByUserIdAsync(profile.UserId);
        UserProfileEntity mergedProfile;

        if (existing != null)
        {
            mergedProfile = CloneProfile(existing);
            mergedProfile.MergeFrom(profile);
        }
        else
        {
            mergedProfile = CloneProfile(profile);
            mergedProfile.CreatedAt = DateTime.UtcNow;
            mergedProfile.ExtractionCount = Math.Max(1, mergedProfile.ExtractionCount);
        }

        FallbackProfiles[profile.UserId] = CloneProfile(mergedProfile);

        var container = await GetContainerAsync();
        if (container == null)
            return;

        try
        {
            await container.UpsertItemAsync(mergedProfile, new PartitionKey(profile.UserId));
            _logger.LogDebug("Upserted profile for user {UserId}, extraction count: {Count}",
                profile.UserId,
                mergedProfile.ExtractionCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist user profile for {UserId}; keeping in-memory fallback",
                profile.UserId);
        }
    }

    public async Task DeleteAsync(string userId)
    {
        FallbackProfiles.TryRemove(userId, out _);

        var container = await GetContainerAsync();
        if (container == null)
            return;

        try
        {
            await container.DeleteItemAsync<UserProfileEntity>(userId, new PartitionKey(userId));
            _logger.LogInformation("Deleted profile for user {UserId}", userId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Profile for user {UserId} not found during delete", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete Cosmos user profile for {UserId}; in-memory fallback already cleared",
                userId);
        }
    }

    public async Task<bool> ExistsAsync(string userId)
    {
        var profile = await GetByUserIdAsync(userId);
        return profile != null;
    }

    private static UserProfileEntity CloneProfile(UserProfileEntity profile)
    {
        return new UserProfileEntity
        {
            Id = profile.Id,
            UserId = profile.UserId,
            Interests = profile.Interests?.ToList(),
            ExperienceLevel = profile.ExperienceLevel,
            TargetRole = profile.TargetRole,
            Constraints = profile.Constraints?.ToList(),
            PersonalityHints = profile.PersonalityHints?.ToList(),
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            ExtractionCount = profile.ExtractionCount
        };
    }
}
