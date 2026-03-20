using Shared.DTOs;

namespace Service.Abstraction;

public interface IUserProfileRepository
{
    Task<UserProfileEntity?> GetByUserIdAsync(string userId);

    Task UpsertAsync(UserProfileEntity profile);

    Task DeleteAsync(string userId);

    Task<bool> ExistsAsync(string userId);
}
