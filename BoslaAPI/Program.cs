using System.Net;
using BoslaAPI.BackgroundServices;
using BoslaAPI.Extensions;
using BoslaAPI.Middlewares;
using Domain.Contracts;
using Domain.Responses;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Data.Contexts;
using Persistence.Data.DataSeeding;
using Persistence.Repositories;
using Service.Extensions;
using Service.Helpers;
using Service.MappingProfiles;
using Shared.Options;

// Load environment variables from .env file
Env.Load("../.env");

var builder = WebApplication.CreateBuilder(args);

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHttpClient(string.Empty, client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddAutoMapper(cfg => { }, typeof(CustomerMapping).Assembly);
builder.Services.AddControllers();
builder.Services.AddAppConfiguration(builder.Configuration);

var connectionStrings = builder.Configuration.GetSection(ConnectionStringsOptions.SectionName).Get<ConnectionStringsOptions>();
var authOptions = builder.Configuration.GetSection(BoslaAuthenticationOptions.SectionName).Get<BoslaAuthenticationOptions>();


builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(connectionStrings.ServerConnection);
});
builder.Services.AddIdentityConfiguration();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddSingleton<AiRequestStore>();
builder.Services.AddHostedService<ChatCleanupBackgroundService>();

builder.Services.AddServices(builder.Configuration);
builder.Services
    .AddJwtConfiguration(builder.Configuration)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookie =>
    {
        var settings = builder.Configuration.GetSection("CookieSettings");
        var sameSite = Enum.TryParse<SameSiteMode>(settings["SameSite"], out var mode) ? mode : SameSiteMode.None;
        var isSecure = !bool.TryParse(settings["Secure"], out var s) || s;
        var isPartitioned = bool.TryParse(settings["Partitioned"], out var p) && p;

        // cookie.Cookie.Domain = "bosla.me";
        cookie.Cookie.SameSite = sameSite;
        cookie.Cookie.SecurePolicy = isSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        cookie.Cookie.HttpOnly = true;

        if (isPartitioned)
        {
            cookie.Cookie.Extensions.Add("Partitioned");
        }
    })
    .AddGoogle("Google", options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = authOptions!.Google.ClientId;
        options.ClientSecret = authOptions.Google.ClientSecret;
        options.CallbackPath = "/api/ExternalAuthentication/signin-google";
        options.ClaimActions.MapJsonKey("urn:google:email_verified", "email_verified");
        options.ClaimActions.MapJsonKey("picture", "picture");
        options.UsePkce = true;
    })
    .AddGitHub("Github", options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = authOptions!.Github.ClientId;
        options.ClientSecret = authOptions.Github.ClientSecret;
        options.CallbackPath = "/api/ExternalAuthentication/signin-github";
        options.Scope.Add("user:email"); // Request email access
        options.ClaimActions.MapJsonKey("urn:github:login", "login");
        options.ClaimActions.MapJsonKey("urn:github:url", "html_url");
        options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
    });




builder.Services.AddRateLimiterConfiguration();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Clear proxy whitelist to trust all proxies (required for Docker behind reverse proxy)
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        corsPolicyBuilder =>
        {
            corsPolicyBuilder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithOrigins(
                    "http://localhost:5173",
                    "https://bosla.me",
                    "https://front.bosla.almiraj.xyz"
                );
        });
});

var app = builder.Build();
await app.DbSeedingAsync();

// IMPORTANT: ForwardedHeaders MUST be first - before any middleware that needs the correct host/scheme
// Uses the pre-configured ForwardedHeadersOptions which clears KnownNetworks/KnownProxies for Docker compatibility
app.UseForwardedHeaders();

if (app.Environment.IsProduction())
{
    app.Use((context, next) =>
    {
        context.Request.Scheme = "https";
        return next();
    });
}

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseMiddleware<ApiResponseMiddleware>();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseTokenRefresh();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results
    .Ok(new APIResponse { StatusCode = HttpStatusCode.OK }))
    .WithTags("Health");

app.Run();
