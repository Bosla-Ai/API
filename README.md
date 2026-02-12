# BoslaAPI

BoslaAPI is the backend service for the [Bosla platform](https://github.com/Bosla-Ai/), designed to generate personalized learning roadmaps and facilitate AI-assisted learning. This project is built using .NET 10 and follows Clean Architecture principles to ensure scalability, maintainability, and testability.

## Technology Stack

The project leverages the following technologies and frameworks:

*   **.NET 10**: The core framework for the application.
*   **ASP.NET Core Web API**: Used for building RESTful endpoints.
*   **Entity Framework Core**: For object-relational mapping.
*   **Dapper**: Micro-ORM for performance-critical queries.
*   **SQL Server**: The primary relational database.
*   **Azure Cosmos DB**: For storing chat history (Optional).
*   **Google Gemini / OpenRouter**: For AI-powered roadmap generation and assistance.
*   **MemoryCache**: For in-memory caching.
*   **Docker**: For containerization.
*   **AutoMapper**: For object-to-object mapping.
*   **Swagger/OpenAPI**: For API documentation and testing.

## Architecture

The solution is structured according to Clean Architecture layers:

*   **BoslaAPI (Presentation Layer)**: Contains the API entry point, controllers, middleware, and configuration. It depends on the Service and Domain layers.
*   **Service (Application Layer)**: Implement business logic, service interfaces, and application-specific rules. It orchestrates data flow between the Presentation and Persistence layers.
*   **Domain (Core Layer)**: The heart of the application, containing entities, value objects, domain exceptions, and repository interfaces. This layer has no dependencies on other layers.
*   **Persistence (Infrastructure Layer)**: Implements database context, repositories, and migrations. It depends on the Domain layer.
*   **Shared**: Contains cross-cutting concerns, constants, and utilities used across multiple layers.

## Prerequisites

Before getting started, ensure you have the following installed:

*   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
*   [Docker Desktop](https://www.docker.com/products/docker-desktop) (Optional, for SQL Server)
*   [Git](https://git-scm.com/)
*   An IDE such as JetBrains Rider or Visual Studio 2022
*   **SQL Server**: Local instance or Docker container.
*   **AI API Keys**: Google Gemini or OpenRouter API keys.

## Getting Started

For detailed platform-specific instructions (Linux, macOS, Windows), refer to the [Development Setup guide in CONTRIBUTING.md](CONTRIBUTING.md#development-setup).

### 1. Clone the Repository

```bash
git clone https://github.com/Bosla-Ai/API.git
cd API
```

### 2. Configure Environment Variables

Create a `.env` file in the root directory based on the provided `.env.example`. This file should contain necessary configuration for database connections, API keys, and other secrets.

```bash
cp .env.example .env
```

Review and update the `.env` file with your local configuration values.

### 3. Run Locally

1.  Ensure **SQL Server** is running.
2.  Ensure using CS connection string in Program.cs.
3.  Update the `.env` file with your connection strings and API keys.
    *   `ConnectionStrings__CS`: Your SQL Server connection string.
    *   `AI__Gemini__ApiKeys`: Your Google Gemini API keys.
    *   `CosmosDb__Endpoint` & `CosmosDb__Key`: (Optional) For chat history.
4.  Restore dependencies and build the project:

```bash
dotnet restore
dotnet build
```

5.  Run the application using either the `https` or `http` launch profile:

```bash
dotnet run --launch-profile https --project BoslaAPI
```

```bash
dotnet run --launch-profile http --project BoslaAPI
```

## API Documentation

The API documentation is available via Swagger UI. Once the application is running, navigate to:

```
http://localhost:5280/swagger
https://localhost:7125/swagger
```

This interface allows you to explore the available endpoints and test them interactively.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
