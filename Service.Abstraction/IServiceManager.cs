namespace Service.Abstraction;

public interface IServiceManager
{
    public IAuthenticationService Authentication { get; }
    public ICustomerService Customer { get; }
    public IRefreshTokenService RefreshToken { get; }
    public IUserService User { get; }
    
    Task SaveChangesAsync();
}