using System.Net;
using AutoMapper;
using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Service.Abstraction;
using Service.Implementations;
using Shared.DTOs.ApplicationUserDTOs;
using Shared.DTOs.LoginDTOs;
using Shared.DTOs.RegisterDTOs;

namespace BoslaAPI.Tests.Services;

/// <summary>
/// Unit tests for AuthenticationService - focusing on UserManager operations
/// that don't require complex helper class dependencies
/// </summary>
public class AuthenticationServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;

    public AuthenticationServiceTests()
    {
        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    #region GetUserByEmailAsync Tests

    [Fact]
    public async Task GetUserByEmailAsync_WithExistingEmail_ReturnsUser()
    {
        // Arrange
        var email = "test@example.com";
        var user = new ApplicationUser { Email = email, UserName = email };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        var service = CreateService();

        // Act
        var result = await service.GetUserByEmailAsync(email);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WithNonExistingEmail_ReturnsNull()
    {
        // Arrange
        var email = "nonexistent@example.com";

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();

        // Act
        var result = await service.GetUserByEmailAsync(email);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region RegisterCustomerAsync Tests

    [Fact]
    public async Task RegisterCustomerAsync_WithNullDto_ThrowsBadRequestException()
    {
        // Arrange
        CustomerRegisterDTO? dto = null;
        var service = CreateService();

        // Act
        var act = async () => await service.RegisterCustomerAsync(dto!);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Customer DTO is null");
    }

    [Fact]
    public async Task RegisterCustomerAsync_WithExistingEmail_ThrowsBadRequestException()
    {
        // Arrange
        var dto = new CustomerRegisterDTO
        {
            Email = "existing@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe"
        };
        var existingUser = new ApplicationUser { Email = dto.Email };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(dto.Email))
            .ReturnsAsync(existingUser);

        var service = CreateService();

        // Act
        var act = async () => await service.RegisterCustomerAsync(dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Customer already exists");
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithNullDto_ThrowsBadRequestException()
    {
        // Arrange
        LoginDTO? dto = null;
        var service = CreateService();

        // Act
        var act = async () => await service.LoginAsync(dto!);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Login data is required.");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ThrowsUnauthorizedException()
    {
        // Arrange
        var dto = new LoginDTO
        {
            Email = "test@example.com",
            Password = "wrongpassword"
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(dto.Email))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();

        // Act
        var act = async () => await service.LoginAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }

    #endregion

    #region GetMeAsync Tests

    [Fact]
    public async Task GetMeAsync_WithValidUserId_ReturnsUser()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, Email = "test@example.com" };
        var userDto = new ApplicationUserDTO { Email = "test@example.com" };

        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var mapperMock = new Mock<IMapper>();
        mapperMock
            .Setup(m => m.Map<ApplicationUserDTO>(user))
            .Returns(userDto);

        var service = CreateService(mapper: mapperMock.Object);

        // Act
        var result = await service.GetMeAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetMeAsync_WithInvalidUserId_ThrowsNotFoundException()
    {
        // Arrange
        var userId = "nonexistent-user";

        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();

        // Act
        var act = async () => await service.GetMeAsync(userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found");
    }

    #endregion

    #region GetUserByIdAsync Tests

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId };

        _userManagerMock
            .Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var service = CreateService();

        // Act
        var result = await service.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
    }

    #endregion

    private AuthenticationService CreateService(IMapper? mapper = null)
    {
        var refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        var customerServiceMock = new Mock<ICustomerService>();
        var unitOfWorkMock = new Mock<Domain.Contracts.IUnitOfWork>();
        var configurationMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
        var roleManagerMock = new Mock<RoleManager<IdentityRole>>(
            roleStoreMock.Object, null!, null!, null!, null!);

        return new AuthenticationService(
            refreshTokenServiceMock.Object,
            customerServiceMock.Object,
            unitOfWorkMock.Object,
            mapper ?? new Mock<IMapper>().Object,
            configurationMock.Object,
            _userManagerMock.Object,
            roleManagerMock.Object,
            null!);  // AuthenticationHelper - skipped for these tests
    }
}
