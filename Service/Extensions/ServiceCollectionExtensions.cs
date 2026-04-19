using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Service.Helpers;
using Service.Implementations;
using Service.Repositories;
using Shared.Options;

namespace Service.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<BoslaAuthenticationOptions>(configuration.GetSection(BoslaAuthenticationOptions.SectionName));
        services.Configure<CosmosDbOptions>(configuration.GetSection(CosmosDbOptions.SectionName));
        services.Configure<ConnectionStringsOptions>(configuration.GetSection(ConnectionStringsOptions.SectionName));
        services.Configure<OAuthSettingsOptions>(configuration.GetSection(OAuthSettingsOptions.SectionName));
        services.Configure<CookieSettingsOptions>(configuration.GetSection(CookieSettingsOptions.SectionName));
        services.Configure<JobMarketOptions>(configuration.GetSection(JobMarketOptions.SectionName));
        services.Configure<StackExchangeOptions>(configuration.GetSection(StackExchangeOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        // Register Cosmos DB client as singleton (lazy initialization - using Options)
        services.AddSingleton<CosmosClient>(sp =>
        {
            var output = sp.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
            var cosmosEndpoint = output.Endpoint;
            var cosmosKey = output.Key;

            if (string.IsNullOrWhiteSpace(cosmosEndpoint) || string.IsNullOrWhiteSpace(cosmosKey))
            {
                return null!;
            }

            return new CosmosClient(cosmosEndpoint, cosmosKey, new CosmosClientOptions
            {
                ApplicationName = "BoslaAPI",
                ConnectionMode = ConnectionMode.Gateway
            });
        });

        // We need to check config to decide which repository to register
        var cosmosEndpoint = configuration["CosmosDb:Endpoint"];
        var cosmosKey = configuration["CosmosDb:Key"];
        var isCosmosConfigured = !string.IsNullOrWhiteSpace(cosmosEndpoint) && !string.IsNullOrWhiteSpace(cosmosKey);

        if (isCosmosConfigured)
        {
            services.AddSingleton<IChatRepository, CosmosChatRepository>();
            services.AddSingleton<IUserProfileRepository, CosmosUserProfileRepository>();
            services.AddSingleton<IFeedbackRepository, CosmosFeedbackRepository>();
        }
        else
        {
            services.AddSingleton<IChatRepository, NullChatRepository>();
            services.AddSingleton<IUserProfileRepository, NullUserProfileRepository>();
            services.AddSingleton<IFeedbackRepository, NullFeedbackRepository>();
        }

        services.AddHttpContextAccessor();

        // Register services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IRoadmapService, RoadmapService>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IJobMarketService, JobMarketService>();
        services.AddHttpClient<StackExchangeHelper>();
        services.AddHttpClient<TechEcosystemHelper>();
        services.AddScoped<IStackExchangeService, StackExchangeService>();
        services.AddScoped<ITechEcosystemService, TechEcosystemService>();
        services.AddScoped<IChatHistoryService, ChatHistoryService>();
        services.AddScoped<IFeedbackService, FeedbackService>();

        services.AddScoped<IServiceManager, ServiceManager>(); // for generalization
        services.AddScoped<AuthenticationHelper>();
        services.AddScoped<CustomerHelper>();
        services.AddSingleton<UserRateLimiter>();
        services.AddScoped<ConversationContextManager>();

        services.AddScoped<IAuthTicketStore, AuthTicketStore>();

        return services;
    }
}
