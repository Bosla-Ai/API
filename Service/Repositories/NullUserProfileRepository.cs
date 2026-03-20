using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

/// <summary>
/// A no-op implementation of IUserProfileRepository used when CosmosDB is not configured.
/// This allows the application to run without CosmosDB for non-AI features.
/// </summary>
public class NullUserProfileRepository : IUserProfileRepository
{
    public Task<UserProfileEntity?> GetByUserIdAsync(string userId)
    {
        return Task.FromResult<UserProfileEntity?>(null);
    }

    public Task UpsertAsync(UserProfileEntity profile)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string userId)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string userId)
    {
        return Task.FromResult(false);
    }
}
