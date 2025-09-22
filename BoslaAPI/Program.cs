using BoslaAPI;
using BoslaAPI.Extensions;
using BoslaAPI.Middlewares;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddAutoMapper(cfg => { }, typeof(CustomerMapping).Assembly);
builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    // option.UseSqlServer(builder.Configuration.GetConnectionString("ServerConnection"));
    option.UseSqlServer(builder.Configuration.GetConnectionString("ServerConnection")); // forDevelopment
});
builder.Services.AddIdentityConfiguration();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddServices();
builder.Services
    .AddJwtConfiguration(builder.Configuration)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookie =>
    {
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddGoogle("Google", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
        options.ClaimActions.MapJsonKey("urn:google:email_verified", "email_verified");
        options.UsePkce = true;
    })
    .AddGitHub("Github", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Github:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Github:ClientSecret"]!;
        options.CallbackPath = "/signin-github";
        options.Scope.Add("user:email"); // Request email access
        options.ClaimActions.MapJsonKey("urn:github:login", "login");
        options.ClaimActions.MapJsonKey("urn:github:url", "html_url");
        options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
    })
    .AddLinkedIn("LinkedIn", options =>
    {
        options.ClientId = builder.Configuration["Authentication:LinkedIn:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:LinkedIn:ClientSecret"]!;
        options.CallbackPath = "/signin-linkedin";
        options.Scope.Add("r_liteprofile");
        options.Scope.Add("r_emailaddress");
        // The provider will populate standard claims (NameIdentifier, Email, Name) with these scopes.
        // You can map extras if you need them:
        // options.ClaimActions.MapCustomJson("urn:linkedin:profileUrl", user => user.GetString("vanityName"));
    });

builder.Services.AddRateLimiterConfiguration();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
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
                .WithOrigins("http://localhost:5173" , "https://www.bosla.almiraj.xyz");
        });
});

var app = builder.Build();
await app.DbSeedingAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ApiResponseMiddleware>();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost
});

app.UseForwardedHeaders();
app.UseCors("CorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();