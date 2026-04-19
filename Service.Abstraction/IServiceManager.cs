namespace Service.Abstraction;

public interface IServiceManager
{
    public IAuthenticationService Authentication { get; }
    public ICustomerService Customer { get; }
    public IRefreshTokenService RefreshToken { get; }
    public IRoadmapService Roadmap { get; }
    public IAdministrationService Administration { get; }
    public IDashboardService Dashboard { get; }
    public IUserService User { get; }
    public IJobMarketService JobMarket { get; }
    public IChatHistoryService ChatHistory { get; }
    public IFeedbackService Feedback { get; }

    Task SaveChangesAsync();
}