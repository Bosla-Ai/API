using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Service.Abstraction;
using Service.Helpers;
using Service.Implementations;
using Service.Repositories;

namespace Service.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        // Register Cosmos DB client as singleton
        services.AddSingleton<CosmosClient>(sp =>
        {
            var endpoint = configuration["CosmosDb:Endpoint"] ?? throw new InvalidOperationException("CosmosDb:Endpoint is not configured");
            var key = configuration["CosmosDb:Key"] ?? throw new InvalidOperationException("CosmosDb:Key is not configured");

            return new CosmosClient(endpoint, key, new CosmosClientOptions
            {
                ApplicationName = "BoslaAPI",
                ConnectionMode = ConnectionMode.Gateway
            });
        });

        // Register repositories
        services.AddScoped<IChatRepository, CosmosChatRepository>();

        // Register services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IRoadmapService, RoadmapService>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IServiceManager, ServiceManager>(); // for generalization
        services.AddScoped<AuthenticationHelper>();
        services.AddScoped<CustomerHelper>();
        services.AddScoped<ConversationContextManager>();

        return services;
    }
}