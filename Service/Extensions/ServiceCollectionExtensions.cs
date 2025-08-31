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
        
        services.AddScoped<IServiceManager, ServiceManager>(); // for generalization
        services.AddScoped<AuthenticationHelper>();
        services.AddScoped<CustomerHelper>();
        return services;
    }
}