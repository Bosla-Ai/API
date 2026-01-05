using Domain.Contracts;
using Service.Abstraction;

namespace Service.Implementations;

public class ServiceManager : IServiceManager
{
    private readonly IUnitOfWork _unitOfWork;

    public IAuthenticationService Authentication { get; private set; }
    public ICustomerService Customer { get; private set; }
    public IRoadmapService Roadmap { get; private set; }
    public IRefreshTokenService RefreshToken { get; }


    public ServiceManager(
        IUnitOfWork unitOfWork,
        IAuthenticationService authentication,
        ICustomerService customer,
        IRoadmapService roadmap,
        IRefreshTokenService refreshToken)
    {
        _unitOfWork = unitOfWork;
        Authentication = authentication;
        Customer = customer;
        Roadmap = roadmap;
        RefreshToken = refreshToken;
    }
    public async Task SaveChangesAsync()
    {
        await _unitOfWork.SaveChangesAsync();
    }
}