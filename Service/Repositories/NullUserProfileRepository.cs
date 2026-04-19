using Microsoft.Extensions.Logging;
using Service.Abstraction;
using Shared.DTOs;

namespace Service.Repositories;

public class NullUserProfileRepository(ILogger<NullUserProfileRepository> logger) : IUserProfileRepository
{
    public Task<UserProfileEntity?> GetByUserIdAsync(string userId)
    {
        return Task.FromResult<UserProfileEntity?>(null);
    }

    public Task UpsertAsync(UserProfileEntity profile)
    {
        logger.LogWarning("UserProfile write discarded — Cosmos DB not configured. UserId: {UserId}", profile.UserId);
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
