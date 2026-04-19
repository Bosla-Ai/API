using Domain.Contracts;
using Service.Abstraction;

namespace Service.Implementations;

public class ServiceManager(
    IUnitOfWork unitOfWork,
    IAuthenticationService authentication,
    ICustomerService customer,
    IRoadmapService roadmap,
    IAdministrationService administration,
    IDashboardService dashboard,
    IRefreshTokenService refreshToken,
    IUserService user,
    IJobMarketService jobMarket,
    IChatHistoryService chatHistory,
    IFeedbackService feedback) : IServiceManager
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public IAuthenticationService Authentication { get; private set; } = authentication;
    public ICustomerService Customer { get; private set; } = customer;
    public IRoadmapService Roadmap { get; private set; } = roadmap;
    public IAdministrationService Administration { get; private set; } = administration;
    public IDashboardService Dashboard { get; private set; } = dashboard;
    public IRefreshTokenService RefreshToken { get; } = refreshToken;
    public IUserService User { get; private set; } = user;
    public IJobMarketService JobMarket { get; private set; } = jobMarket;
    public IChatHistoryService ChatHistory { get; private set; } = chatHistory;
    public IFeedbackService Feedback { get; private set; } = feedback;

    public async Task SaveChangesAsync()
    {
        await _unitOfWork.SaveChangesAsync();
    }
}