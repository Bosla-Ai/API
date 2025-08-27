using Domain.Entities;

namespace Service.Abstraction;

public interface IApplicationUserService
{
    Task<IEnumerable<ApplicationUser>> GetAllAsync();
    Task<ApplicationUser> GetByIdAsync(string applicationUserId);
    Task CreateAsync(ApplicationUser applicationUser);
    Task UpdateAsync(ApplicationUser applicationUser);
    Task DeleteAsync(ApplicationUser applicationUser);
}