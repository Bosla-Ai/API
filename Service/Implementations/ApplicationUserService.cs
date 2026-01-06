using Domain.Contracts;
using Domain.Entities;
using Service.Abstraction;

namespace Service.Implementations;

public sealed class ApplicationUserService : IApplicationUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public ApplicationUserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllAsync()
    {
        return await _unitOfWork.GetRepo<ApplicationUser, string>()
            .GetAllAsync();
    }

    public async Task<ApplicationUser> GetByIdAsync(string applicationUserId)
    {
        return await _unitOfWork.GetRepo<ApplicationUser, string>()
            .GetIdAsync(applicationUserId);
    }

    public async Task CreateAsync(ApplicationUser applicationUser)
    {
        await _unitOfWork.GetRepo<ApplicationUser, ApplicationUser>()
            .CreateAsync(applicationUser);
    }

    public async Task UpdateAsync(ApplicationUser applicationUser)
    {
        _unitOfWork.GetRepo<ApplicationUser, ApplicationUser>()
            .UpdateAsync(applicationUser);
    }

    public async Task DeleteAsync(ApplicationUser applicationUser)
    {
        _unitOfWork.GetRepo<ApplicationUser, ApplicationUser>()
            .DeleteAsync(applicationUser);
    }
}