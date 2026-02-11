using Domain.Contracts;
using Service.Abstraction;

namespace Service.Implementations;

public class ServiceManager : IServiceManager
{
    private readonly IUnitOfWork _unitOfWork;

    public IAuthenticationService Authentication { get; private set; }
    public ICustomerService Customer { get; private set; }
    public IRoadmapService Roadmap { get; private set; }
    public IAdministrationService Administration { get; private set; }
    public IDashboardService Dashboard { get; private set; }
    public IRefreshTokenService RefreshToken { get; }
    public IUserService User { get; private set; }
    public IJobMarketService JobMarket { get; private set; }
    public IChatHistoryService ChatHistory { get; private set; }


    public ServiceManager(
        IUnitOfWork unitOfWork,
        IAuthenticationService authentication,
        ICustomerService customer,
        IRoadmapService roadmap,
        IAdministrationService administration,
        IDashboardService dashboard,
        IRefreshTokenService refreshToken,
        IUserService user,
        IJobMarketService jobMarket,
        IChatHistoryService chatHistory)
    {
        _unitOfWork = unitOfWork;
        Authentication = authentication;
        Customer = customer;
        Roadmap = roadmap;
        Administration = administration;
        Dashboard = dashboard;
        RefreshToken = refreshToken;
        User = user;
        JobMarket = jobMarket;
        ChatHistory = chatHistory;
    }
    public async Task SaveChangesAsync()
    {
        await _unitOfWork.SaveChangesAsync();
    }
}