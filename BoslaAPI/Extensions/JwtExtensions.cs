using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Shared;

namespace BoslaAPI.Extensions;

public static class JwtExtensions
{
    public static AuthenticationBuilder AddJwtConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("JWT").Get<JwtOptions>();

        return services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions!.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Key)
                    ),
                    // ClockSkew: Allows for minor clock differences between servers.
                    // Default is 5 minutes, reduced to 1 minute for tighter security.
                    // This means a token is still valid up to 1 minute after expiration
                    // to account for network delays and server time differences.
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context?.Token)
                            && context!.Request.Cookies
                                .ContainsKey(StaticData.AccessToken))
                        {
                            context.Token = context.Request.Cookies[StaticData.AccessToken];
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }
}