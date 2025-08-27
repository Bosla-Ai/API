using Domain.Contracts;
using Service.Abstraction;

namespace Service.Implementations;

public class UnitOfService :  IUnitOfService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public IAuthenticationService Authentication { get; private set; }
    public ICustomerService Customer { get; private set; }
    public IRefreshTokenService RefreshToken { get; }
    public IUserService User { get; }


    public UnitOfService(
        IUnitOfWork unitOfWork,
        IAuthenticationService authentication,
        ICustomerService customer,
        IRefreshTokenService refreshToken,
        IUserService user)
    {
        _unitOfWork = unitOfWork;
        Authentication = authentication;
        Customer = customer;
        RefreshToken = refreshToken;
        User = user;
    }
    public async Task SaveChangesAsync()
    {
         await _unitOfWork.SaveChangesAsync();
    }
}