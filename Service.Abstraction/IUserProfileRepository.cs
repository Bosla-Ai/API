using Shared.DTOs;

namespace Service.Abstraction;

/// <summary>
/// Repository interface for user profile storage in Cosmos DB.
/// Profiles are extracted from AI conversations and used to personalize responses.
/// </summary>
public interface IUserProfileRepository
{
    /// <summary>
    /// Get the profile for a specific user
    /// </summary>
    Task<UserProfileEntity?> GetByUserIdAsync(string userId);

    /// <summary>
    /// Insert or update a user profile (smart merge if exists)
    /// </summary>
    Task UpsertAsync(UserProfileEntity profile);

    /// <summary>
    /// Delete a user's profile
    /// </summary>
    Task DeleteAsync(string userId);

    /// <summary>
    /// Check if a user has a profile
    /// </summary>
    Task<bool> ExistsAsync(string userId);
}
