# Contributing to BoslaAPI

Thank you for your interest in contributing to BoslaAPI. This document outlines the guidelines and standards for contributing to this project. We appreciate your efforts to improve the code quality and functionality.

## Code of Conduct

All contributors are expected to maintain a professional and respectful environment. Harassment, discrimination, or offensive language will not be tolerated. Please focus on constructive technical discussion and collaboration.

## Development Setup

To set up your development environment, please follow the platform-specific instructions below.

### Linux & macOS

1.  **Install Prerequisites**:
    *   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
    *   [Docker Desktop](https://www.docker.com/products/docker-desktop) or Docker Engine

2.  **Run SQL Server (Docker)**:
    Start a SQL Server container with the following command (replace `YourStrongPassword123`):

    ```bash
    docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrongPassword123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
    ```

3.  **Clone & Configure**:
    ```bash
    git clone https://github.com/Bosla-Ai/API.git
    cd API
    cp .env.example .env
    ```
    Update `.env` with your SQL Server password and AI API keys.

4.  **Build & Run**:
    ```bash
    dotnet build
    dotnet run --launch-profile https --project BoslaAPI
    ```

### Windows

1.  **Install Prerequisites**:
    *   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
    *   [Visual Studio 2022](https://visualstudio.microsoft.com/) or [Docker Desktop](https://www.docker.com/products/docker-desktop)

2.  **SQL Server**:
    *   Use **LocalDB** (installed with Visual Studio) or
    *   Run SQL Server via Docker Desktop (same command as Linux/macOS).

3.  **Clone & Configure**:
    ```powershell
    git clone https://github.com/Bosla-Ai/API.git
    cd API
    copy .env.example .env
    ```
    Update `.env` with your connection string.

4.  **Build & Run**:
    Open the solution in Visual Studio and press F5, or run via terminal:
    ```powershell
    dotnet build
    dotnet run --launch-profile https --project BoslaAPI
    ```

## Coding Standards

We adhere to strict coding standards to ensure consistency and maintainability.

### General Guidelines

*   **Clean Architecture**: strictly follow the dependency rules. Domain layer must not depend on any outer layers.
*   **C# Conventions**: Follow standard C# coding conventions (PascalCase for classes/methods, camelCase for local variables/parameters).
*   **Simplicity**: Choose simple solutions over complex ones. Avoid over-engineering.
*   **Readability**: Write self-documenting code. Use descriptive variable and method names.
*   **Single Responsibility**: Classes and methods should have a single responsibility.
*   **No Magic Numbers**: Use named constants instead of hardcoded values.

### File Structure

*   Keep files small (under 500 lines). Break down large classes into smaller, logical components.
*   Organize files into folders by feature or responsibility.

### Commenting

*   Write clear comments for public APIs and complex logic.
*   Avoid redundant comments that simply restate the code.
*   Do not leave commented-out code in the repository.

## Pull Request Process

1.  **Find an Issue**: Ensure there is an open issue for the task you want to work on. If not, open one first.
2.  **Get Assigned**: Do not open a Pull Request unless the issue has been assigned to you.
3.  **Create a Branch**: Create a new branch for your feature or bug fix from the `main` branch. Use a descriptive name (e.g., `feature/add-roadmap-generation`, `fix/user-authentication`).
4.  **Implement Changes**: Make your changes, ensuring they follow the coding standards.
5.  **Run Tests**: Run all unit and integration tests to ensure no regressions.
6.  **Submit PR**: Push your branch and open a Pull Request against the `main` branch.
7.  **Description**:
    *   Reference the issue you are solving (e.g., `Closes #123`).
    *   Provide a detailed description of the changes.
    *   Explain the approach and any design decisions.
8.  **Review**: Address any feedback from code reviewers.

## Issue Reporting

If you encounter a bug or have a feature request, please open an issue in the issue tracker.

*   **Bugs**: Fully describe the problem, including the exact location (file/method), steps to reproduce, expected behavior, and actual behavior.
*   **Features**: Describe the proposed feature, its benefits, and where it fits in the architecture.

Thank you for contributing to BoslaAPI.
