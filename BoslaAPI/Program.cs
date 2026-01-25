using System.Security.Claims;
using BoslaAPI;
using BoslaAPI.Extensions;
using BoslaAPI.Middlewares;
using DotNetEnv;
using Domain.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Data.Contexts;
using Persistence.Data.DataSeeding;
using Persistence.Repositories;
using Persistence.Seeder;
using Service.Abstraction;
using Service.Extensions;
using Service.Implementations;
using Service.MappingProfiles;

// Load environment variables from .env file
Env.Load("../.env");

var builder = WebApplication.CreateBuilder(args);

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHttpClient();

builder.Services.AddAutoMapper(cfg => { }, typeof(CustomerMapping).Assembly);
builder.Services.AddControllers();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Bosla_";
});
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    option.UseSqlServer(builder.Configuration.GetConnectionString("ServerConnection"));
    // option.UseSqlServer(builder.Configuration.GetConnectionString("CS")); // forDevelopment
});
builder.Services.AddHttpClient();
builder.Services.AddIdentityConfiguration();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddServices();
builder.Services
    .AddJwtConfiguration(builder.Configuration)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookie =>
    {
        cookie.Cookie.SameSite = SameSiteMode.None;
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookie.Cookie.HttpOnly = true;
    })
    .AddCookie("Identity.External", options =>
    {
        options.Cookie.Name = ".AspNetCore.Identity.External";
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddGoogle("Google", options =>
    {
        options.SignInScheme = "Identity.External";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/api/ExternalAuthentication/signin-google";
        options.ClaimActions.MapJsonKey("urn:google:email_verified", "email_verified");
        options.UsePkce = true;
    })
    .AddGitHub("Github", options =>
    {
        options.SignInScheme = "Identity.External";
        options.ClientId = builder.Configuration["Authentication:Github:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Github:ClientSecret"]!;
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
    options.KnownNetworks.Clear();
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
                .WithOrigins("http://localhost:5173", "https://front.bosla.almiraj.xyz", "https://bosla.me");
        });
});

var app = builder.Build();
await app.DbSeedingAsync();

// IMPORTANT: ForwardedHeaders MUST be first - before any middleware that needs the correct host/scheme
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost
});

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseMiddleware<ApiResponseMiddleware>();
app.UseCors("CorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();