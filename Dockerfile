FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["BoslaAPI/BoslaAPI.csproj", "BoslaAPI/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Persistence/Persistence.csproj", "Persistence/"]
COPY ["Presentation/Presentation.csproj", "Presentation/"]
COPY ["Service/Service.csproj", "Service/"]
COPY ["Service.Abstraction/Service.Abstraction.csproj", "Service.Abstraction/"]
COPY ["Shared/Shared.csproj", "Shared/"]

# Restore dependencies
RUN dotnet restore "BoslaAPI/BoslaAPI.csproj"

# Copy source code
COPY . .
WORKDIR "/src/BoslaAPI"
RUN dotnet build "BoslaAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BoslaAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoslaAPI.dll"]
