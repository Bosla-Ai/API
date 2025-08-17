namespace Service.Abstraction;

public interface IUnitOfService
{
    public IAuthenticationService Authentication { get; }
    public ICustomerService Customer { get; }
    public IRefreshTokenService RefreshToken { get; }
    
    Task SaveChangesAsync();
}