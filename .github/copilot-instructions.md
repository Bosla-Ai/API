# Bosla AI API - Developer Instructions

## Quick Start
**.NET 8 Clean Architecture API with AI integration (Gemini), OAuth authentication, and EF Core.**

**⚠️ Current State**: Build fails due to AutoMapper dependency issue in `Service/Service.csproj` - replace DLL reference with NuGet package reference.

## Architecture Overview

This is a .NET 8 Web API following **Clean Architecture** with these layers:
- **Domain**: Core entities, contracts, specifications (`Domain/`)
- **Service**: Business logic with abstractions (`Service/`, `Service.Abstraction/`)
- **Persistence**: Data access with EF Core (`Persistence/`)
- **Presentation**: Controllers and API endpoints (`Presentation/`)
- **BoslaAPI**: Main startup project with configuration
- **Shared**: DTOs and cross-cutting models

## Critical Patterns

### Repository + Unit of Work Pattern
```csharp
// Access repositories through UnitOfWork in services
var customer = await unitOfWork.GetRepo<Customer, string>().GetIdAsync(id);
await unitOfWork.SaveChangesAsync();
```

### Specifications Pattern
Create complex queries using `Specifications<TEntity>` base class:
```csharp
public class CustomerDetailsSpecification : Specifications<Customer>
{
    public CustomerDetailsSpecification(string customerId) 
        : base(c => c.Id == customerId)
    {
        AddInclude(c => c.SomeRelatedEntity);
    }
}
```

### Service Manager Pattern
All services are accessed through `IServiceManager`:
```csharp
public class UserController(IServiceManager serviceManager) : ApiController
{
    var result = await serviceManager.Customer.ProcessUserQueryAsync(query);
}
```

### API Response Wrapper
All responses are automatically wrapped by `APIResponseMiddleware`:
```csharp
// Controller returns raw data
return Ok(customerData);
// Client receives: { "statusCode": 200, "isSuccess": true, "data": customerData }
```

## Key Integrations

### AI Processing (Gemini API)
- Configuration: `Gemini:ApiKey` and `Gemini:ApiUrl` in appsettings
- Implementation: `CustomerHelper.SendRequestToGemini()` 
- Endpoint: `POST /api/User/ask-ai` with `AiQueryRequest`

### External Authentication
OAuth providers configured in `Program.cs`:
- Google: `/signin-google`
- GitHub: `/signin-github` 
- LinkedIn: `/signin-linkedin`

### Database
- SQL Server with EF Core
- Connection string: `CS` (development) or `ServerConnection` (production)
- Migrations: `dotnet ef migrations add <name> --project Persistence`

## Development Workflows

### Build & Run
```bash
dotnet restore
dotnet build    # ⚠️ Currently fails due to AutoMapper dependency issue
dotnet run --project BoslaAPI
```

### Current Build Issues
- AutoMapper dependency error in Service project (see AutoMapper section for fix)
- 75+ nullable reference warnings (non-breaking)
- GitHub OAuth package version mismatch warning (non-breaking)

### Database Setup
```bash
# Add migration
dotnet ef migrations add <MigrationName> --project Persistence --startup-project BoslaAPI

# Update database
dotnet ef database update --project Persistence --startup-project BoslaAPI
```

### CORS Configuration
Frontend expected at `http://localhost:5173` (React/Vite)

## Project-Specific Conventions

### Dependency Injection
- Services registered in `Service/Extensions/ServiceCollectionExtensions.cs`
- Extensions in `BoslaAPI/Extensions/` for specific configurations
- All services use constructor injection with primary constructors

### Error Handling
Custom exceptions in `Domain/Exceptions/`:
- `BadRequestException`, `UnauthorizedException`, `NotFoundException`, `InternalServerErrorException`
- Automatically handled by `APIResponseMiddleware`

### Entity Configuration
- EF configurations in `Persistence/Data/Configurations/`
- Enum conversions: `builder.Property(r => r.Level).HasConversion<string>()`
- Use `ApplicationDbContext` for all database operations

### AutoMapper Integration
- Profiles in `Service/MappingProfiles/`
- Registered in `Program.cs`: `AddAutoMapper(typeof(CustomerMapping).Assembly)`
- **⚠️ CRITICAL**: Currently has build-breaking dependency issues
  - AutoMapper referenced as DLL instead of NuGet package in `Service/Service.csproj`
  - To fix: Replace `<Reference Include="AutoMapper">` with `<PackageReference Include="AutoMapper" Version="15.0.1" />`

## Common Tasks

### Adding New Entity
1. Create entity in `Domain/Entities/`
2. Add configuration in `Persistence/Data/Configurations/`
3. Create migration: `dotnet ef migrations add Add<EntityName>`
4. Add to `ApplicationDbContext.DbSet<Entity>`

### Adding New Service
1. Interface in `Service.Abstraction/I<ServiceName>.cs`
2. Implementation in `Service/Implementations/<ServiceName>.cs` 
3. Register in `ServiceCollectionExtensions.AddServices()`
4. Add to `IServiceManager` interface and implementation

### Adding New Controller
1. Inherit from `ApiController` base class
2. Use `IServiceManager` for business logic
3. Return raw data (middleware handles response wrapping)
4. Place in `Presentation/Controllers/`

## Key Files Reference

### Startup & Configuration
- `BoslaAPI/Program.cs`: Main startup, DI, authentication, middleware pipeline
- `BoslaAPI/appsettings.json`: Database, OAuth, API key configuration
- `global.json`: .NET 8.0 SDK version lock

### Service Layer Structure
- `Service.Abstraction/I*.cs`: Service interfaces
- `Service/Implementations/*.cs`: Business logic implementations  
- `Service/Helpers/CustomerHelper.cs`: Gemini AI API integration
- `Service/Extensions/ServiceCollectionExtensions.cs`: DI registration

### Data Layer Structure  
- `Persistence/Data/Contexts/ApplicationDbContext.cs`: EF Core DbContext
- `Persistence/Data/Configurations/*.cs`: Entity configurations
- `Persistence/Repositories/`: Repository pattern implementations
- `Domain/Entities/*.cs`: Core business entities
- `Domain/Contracts/Specifications.cs`: Query specification base class

### API Layer Structure
- `Presentation/Controllers/ApiController.cs`: Base controller with common attributes
- `BoslaAPI/Middlewares/APIResponseMiddleware.cs`: Auto-wraps all responses
- `Shared/`: DTOs and request/response models

## Configuration Files
- `appsettings.json`: Database connections, API keys, authentication settings
- Required config sections: `ConnectionStrings`, `Authentication`, `Gemini`