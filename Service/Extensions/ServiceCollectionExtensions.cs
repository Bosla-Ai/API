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

        // Register Cosmos DB client as singleton (lazy initialization - only if configured)
        var cosmosEndpoint = configuration["CosmosDb:Endpoint"];
        var cosmosKey = configuration["CosmosDb:Key"];
        var isCosmosConfigured = !string.IsNullOrWhiteSpace(cosmosEndpoint) && !string.IsNullOrWhiteSpace(cosmosKey);

        if (isCosmosConfigured)
        {
            services.AddSingleton<CosmosClient>(sp =>
            {
                return new CosmosClient(cosmosEndpoint, cosmosKey, new CosmosClientOptions
                {
                    ApplicationName = "BoslaAPI",
                    ConnectionMode = ConnectionMode.Gateway
                });
            });

            // Register repositories
            services.AddScoped<IChatRepository, CosmosChatRepository>();
        }
        else
        {
            // Register null/no-op repository when CosmosDB is not configured
            services.AddScoped<IChatRepository, NullChatRepository>();
        }

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