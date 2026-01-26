FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first (changes less frequently = better caching)
COPY ["BoslaAPI/BoslaAPI.csproj", "BoslaAPI/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Persistence/Persistence.csproj", "Persistence/"]
COPY ["Presentation/Presentation.csproj", "Presentation/"]
COPY ["Service/Service.csproj", "Service/"]
COPY ["Service.Abstraction/Service.Abstraction.csproj", "Service.Abstraction/"]
COPY ["Shared/Shared.csproj", "Shared/"]

# Restore dependencies (cached if .csproj files unchanged)
RUN dotnet restore "BoslaAPI/BoslaAPI.csproj"

# Copy source code (changes frequently)
COPY . .
WORKDIR "/src/BoslaAPI"
RUN dotnet build "BoslaAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BoslaAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Set Production environment (ignores appsettings.Development.json)
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoslaAPI.dll"]
