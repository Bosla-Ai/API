using Microsoft.Extensions.DependencyInjection;
using Service.Abstraction;
using Service.Helpers;
using Service.Implementations;

namespace Service.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IUserService, UserService>();
        
        services.AddScoped<IUnitOfService, UnitOfService>(); // for generalization
        services.AddScoped<AuthenticationHelper>();
        return services;
    }
}